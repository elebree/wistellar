import type { Locator } from "@playwright/test";
import { test, expect, stubToken } from "./stub-api";
import { menuButton, openPanelAtCenter, panel, visitMap } from "./map";

/** Whether the element is what a tap at its center would hit, i.e. nothing covers it. */
const isOnTop = (locator: Locator) =>
  locator.evaluate((el) => {
    const r = el.getBoundingClientRect();
    const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
    return hit !== null && el.contains(hit);
  });

test.describe("phone layout", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("keeps the filter box and the menu button side by side in the top row", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await expect(page.locator("#filter")).toBeVisible();
    const filter = (await page.locator("#filter").boundingBox())!;
    const button = (await menuButton(page).boundingBox())!;
    expect(filter.x + filter.width, "filter ends before the button").toBeLessThanOrEqual(button.x);
    expect(Math.abs(filter.y - button.y), "same top").toBeLessThan(1);
    expect(Math.abs(filter.height - button.height), "same height").toBeLessThan(1);
    expect(Math.abs(button.x + button.width - (390 - 10)), "10px from the right edge").toBeLessThan(1);
  });

  test("shows network details full width across the top, under the filter row", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await openPanelAtCenter(page);
    const box = (await panel(page).boundingBox())!;
    expect(box.x, "flush left").toBe(0);
    expect(box.y, "flush with the top").toBe(0);
    expect(box.width, "full width").toBe(390);
    await expect(panel(page), "square corners").toHaveCSS("border-radius", "0px");

    const filter = page.locator("#filter");
    const filterBox = (await filter.boundingBox())!;
    const bssid = (await panel(page).getByText("00:00:00:00:00:01", { exact: true }).boundingBox())!;
    expect(bssid.y, "details start below the filter row").toBeGreaterThan(filterBox.y + filterBox.height);

    expect(await isOnTop(filter), "filter box drawn over the panel").toBe(true);
    expect(await isOnTop(menuButton(page)), "menu button drawn over the panel").toBe(true);
    await menuButton(page).click();
    expect(await isOnTop(page.getByRole("menu")), "open menu drawn over the panel").toBe(true);
  });
});
