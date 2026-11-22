import type { Locator } from "@playwright/test";
import { test, expect, stubToken } from "./stub-api";
import { style } from "./stub-data";
import { filtersButton, menuButton, openPanelAtCenter, panel, visitMap } from "./map";

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

  test("fits the funnel under the filter box", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    const filter = (await page.locator("#filter").boundingBox())!;
    const funnel = (await filtersButton(page).boundingBox())!;
    expect(funnel.x, "lines up with the filter box").toBeCloseTo(filter.x, 0);
    expect(funnel.y - (filter.y + filter.height), "10px below the filter box").toBeCloseTo(10, 0);
    expect(funnel.height, "as high as the filter box").toBeCloseTo(filter.height, 0);
  });

  test("shows network details full width across the top, in place of the filter row", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await openPanelAtCenter(page);
    const box = (await panel(page).boundingBox())!;
    expect(box.x, "flush left").toBe(0);
    expect(box.y, "flush with the top").toBe(0);
    expect(box.width, "full width").toBe(390);
    await expect(panel(page), "square corners").toHaveCSS("border-radius", "0px");

    await expect(page.locator("#filter"), "no filter box while the details are open").toBeHidden();
    await expect(filtersButton(page), "no funnel while the details are open").toBeHidden();

    // The menu button stays, over the panel's top right corner, with the close button beside it.
    const menu = (await menuButton(page).boundingBox())!;
    const close = panel(page).getByRole("button", { name: "Close" });
    const closeBox = (await close.boundingBox())!;
    expect(closeBox.x + closeBox.width, "close button ends 10px before the menu button").toBeCloseTo(menu.x - 10, 0);
    expect(closeBox.y, "same top as the menu button").toBeCloseTo(menu.y, 0);
    expect(closeBox.height, "as high as the menu button").toBeCloseTo(menu.height, 0);
    expect(await isOnTop(close), "close button not covered").toBe(true);

    const bssid = (await panel(page).getByText("00:00:00:00:00:01", { exact: true }).boundingBox())!;
    expect(bssid.y, "details start in the menu button's row").toBeLessThan(menu.y + menu.height);

    expect(await isOnTop(menuButton(page)), "menu button drawn over the panel").toBe(true);
    await menuButton(page).click();
    expect(await isOnTop(page.getByRole("menu")), "open menu drawn over the panel").toBe(true);
  });

  test("brings the filter row back when the details close", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await openPanelAtCenter(page);
    await expect(page.locator("#filter")).toBeHidden();
    await panel(page).getByRole("button", { name: "Close" }).click();
    await expect(panel(page)).toHaveCount(0);
    await expect(page.locator("#filter")).toBeVisible();
    await expect(filtersButton(page)).toBeVisible();
    expect(await isOnTop(page.locator("#filter")), "filter box usable again").toBe(true);
  });
});

test.describe("map attribution on a phone", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  // The stub style has no sources, so nothing to credit; this one has.
  const creditedStyle = {
    ...style,
    sources: {
      credits: { type: "geojson", data: { type: "FeatureCollection", features: [] }, attribution: "Stub Maps" },
    },
    layers: [...style.layers, { id: "credits", type: "circle", source: "credits" }],
  };

  test("stays collapsed after the user collapsed it, across launches", async ({ page }) => {
    await page.route("**/liberty.json", (route) => route.fulfill({ json: creditedStyle }));
    const attribution = page.locator(".maplibregl-ctrl-attrib");

    await visitMap(page, { token: stubToken() });
    await expect(attribution).toHaveClass(/maplibregl-compact-show/);
    await expect(attribution).toContainText("Stub Maps");

    await page.locator(".maplibregl-ctrl-attrib-button").click();
    await expect(attribution).not.toHaveClass(/maplibregl-compact-show/);

    await page.reload();
    await expect(attribution).toHaveClass(/maplibregl-compact/);
    await expect(attribution).toContainText("Stub Maps");
    await expect(attribution).not.toHaveClass(/maplibregl-compact-show/);

    // Expanding it again is remembered too.
    await page.locator(".maplibregl-ctrl-attrib-button").click();
    await expect(attribution).toHaveClass(/maplibregl-compact-show/);
    await page.reload();
    await expect(attribution).toHaveClass(/maplibregl-compact-show/);
  });
});
