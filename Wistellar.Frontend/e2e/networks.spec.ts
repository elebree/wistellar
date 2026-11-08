import { test, expect, isTile, stubToken } from "./stub-api";
import {
  canvas,
  detail,
  dialog,
  menuButton,
  openPanelAtCenter,
  panel,
  visitMap,
  waitForNetworkAtCenter,
  zoomOut,
} from "./map";

test.describe("networks, signed in", () => {
  const token = stubToken();

  test("sends the token with tile requests but not with the base map style", async ({ page, api }) => {
    const tileRequest = page.waitForRequest((request) => isTile(new URL(request.url())));
    await visitMap(page, { token });
    expect((await (await tileRequest).allHeaders()).authorization).toBe(`Bearer ${token}`);
    expect(await api.style[0].allHeaders()).not.toHaveProperty("authorization");
  });

  test("opens a panel with the network's details", async ({ page }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    await expect(panel(page)).toHaveCount(1);
    await expect(panel(page).getByText("00:00:00:00:00:01", { exact: true })).toBeVisible();
    await expect(panel(page).getByText("Stub WiFi One", { exact: true })).toBeVisible();
    await expect(panel(page).locator("span.bg-green-50")).toHaveText(["WPA2-PSK-CCMP", "ESS"]);
    await expect(detail(page, "Last seen")).toHaveText(/^\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$/);
    await expect(detail(page, "Dwell")).toHaveText("1 h 2 min");
    await expect(detail(page, "Range")).toHaveText("0");
    await expect(detail(page, "Vendor")).toHaveText("Stub Vendor");
  });

  test("shows the details on the left under the filter box, not at the clicked point", async ({ page }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    const filter = (await page.locator("#filter").boundingBox())!;
    const box = (await panel(page).boundingBox())!;
    expect(Math.abs(box.x - filter.x), "left edge lines up with the filter box").toBeLessThan(1);
    expect(box.y - (filter.y + filter.height), "10px below the filter box").toBeCloseTo(10, 0);
    expect(box.x + box.width, "ends left of the clicked point at the center").toBeLessThan(640);
  });

  test("opens one panel where networks of two layers overlap", async ({ page, api }) => {
    await visitMap(page, { at: "00:00:00:00:00:02", token });
    await waitForNetworkAtCenter(page);
    await canvas(page).click();
    await expect(panel(page)).toHaveCount(1);
    await page.waitForTimeout(500);
    expect(api.networkInfo).toHaveLength(1);
  });

  test("shows markup in an SSID as text", async ({ page }) => {
    await visitMap(page, { at: "00:00:00:00:00:04", token });
    await openPanelAtCenter(page);
    const ssid = '<b>Stub & "Markup"</b><img src=x onerror=window.__xss=1>';
    await expect(panel(page).getByText(ssid, { exact: true })).toBeVisible();
    await expect(panel(page).locator("b, img")).toHaveCount(0);
    expect(await page.evaluate(() => (window as { __xss?: number }).__xss)).toBeUndefined();
  });

  test("draws the details panel with the same style as the menu", async ({ page }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    await menuButton(page).click();
    const panelStyle = (el: Element) => {
      const style = getComputedStyle(el);
      return [style.borderRadius, style.padding, style.boxShadow];
    };
    const menuStyle = await page.getByRole("menu").locator("xpath=..").evaluate(panelStyle);
    expect(await panel(page).evaluate(panelStyle)).toEqual(menuStyle);
  });

  test("closes the panel with its button and with a click elsewhere on the map", async ({ page }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    await panel(page).getByRole("button", { name: "Close" }).click();
    await expect(panel(page)).toHaveCount(0);

    await openPanelAtCenter(page);
    await canvas(page).click({ position: { x: 150, y: 650 } });
    await expect(panel(page)).toHaveCount(0);
  });

  test("signs out: removes panel and filter, forgets the token, stops loading networks", async ({ page, api }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    await menuButton(page).click();
    await page.getByRole("menuitem", { name: "Sign out" }).click();

    await expect(panel(page)).toHaveCount(0);
    await expect(page.locator("#filter")).toHaveCount(0);
    expect(await page.evaluate(() => localStorage.getItem("access_token"))).toBeNull();
    const before = api.geo.length;
    await zoomOut(page);
    await page.waitForTimeout(1000);
    expect(api.geo).toHaveLength(before);
  });

  test("signs out on a tile 401 and asks to sign in again, once", async ({ page, api }) => {
    // The token is no longer accepted, so the first tiles already answer 401.
    await page.route(isTile, (route) => route.fulfill({ status: 401 }));
    await visitMap(page, { token });
    await expect(dialog(page)).toHaveAttribute("open");
    await expect(page.locator('dialog [role="status"]')).toContainText("Your session has expired");
    await expect(page.locator("#filter")).toHaveCount(0);

    // Dismissed, it stays closed and nothing asks for tiles any more.
    await page.keyboard.press("Escape");
    await expect(dialog(page)).not.toHaveAttribute("open");
    const before = api.geo.length;
    await zoomOut(page);
    await page.waitForTimeout(1000);
    expect(api.geo).toHaveLength(before);
    await expect(dialog(page)).not.toHaveAttribute("open");
  });
});
