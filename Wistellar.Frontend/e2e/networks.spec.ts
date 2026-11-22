import { test, expect, isTile, stubToken } from "./stub-api";
import {
  canvas,
  detail,
  dialog,
  menuButton,
  openPanelAtCenter,
  panel,
  filtersButton,
  openFilters,
  typeToggles,
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
    // The type as the same badge as its toggle.
    const type = panel(page).getByRole("img", { name: "WiFi", exact: true });
    await expect(type).toHaveClass(/type-badge/);
    await expect(type).toHaveAttribute("style", /--type-color: tomato/);
    await openFilters(page);
    const toggleTint = await typeToggles(page)
      .getByRole("button", { name: "WiFi", exact: true })
      .evaluate((el) => getComputedStyle(el).backgroundColor);
    await expect(type, "same tint as the WiFi toggle").toHaveCSS("background-color", toggleTint);
    const box = (await type.boundingBox())!;
    expect([box.width, box.height], "the toggle's square").toEqual([32, 32]);
    await expect(panel(page).locator("span.bg-green-50")).toHaveText(["WPA2-PSK-CCMP", "ESS"]);
    await expect(detail(page, "Last seen")).toHaveText(/^\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$/);
    await expect(detail(page, "Seen over")).toHaveText("1 h 2 min");
    await expect(detail(page, "Seen across")).toHaveText("0 m");
    await expect(detail(page, "Sightings")).toHaveText("3");
    await expect(detail(page, "Vendor")).toHaveText("Stub Vendor");
  });

  test("shows the details on the left under the funnel's row, not at the clicked point", async ({ page }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    // Only phones give the filter row's place to the details.
    await expect(page.locator("#filter")).toBeVisible();
    await expect(filtersButton(page)).toBeVisible();
    const filter = (await page.locator("#filter").boundingBox())!;
    const funnel = (await filtersButton(page).boundingBox())!;
    const box = (await panel(page).boundingBox())!;
    expect(Math.abs(box.x - filter.x), "left edge lines up with the filter box").toBeLessThan(1);
    expect(box.y - (funnel.y + funnel.height), "10px below the funnel").toBeCloseTo(10, 0);
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
