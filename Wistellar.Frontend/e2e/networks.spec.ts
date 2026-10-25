import { test, expect, isTile, stubToken } from "./stub-api";
import {
  canvas,
  detail,
  dialog,
  menuButton,
  openPopupAtCenter,
  popup,
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

  test("opens a popup with the network's details", async ({ page }) => {
    await visitMap(page, { token });
    await openPopupAtCenter(page);
    await expect(popup(page)).toHaveCount(1);
    await expect(popup(page).getByText("00:00:00:00:00:01", { exact: true })).toBeVisible();
    await expect(popup(page).getByText("Stub WiFi One", { exact: true })).toBeVisible();
    await expect(popup(page).locator("span.bg-green-50")).toHaveText(["WPA2-PSK-CCMP", "ESS"]);
    await expect(detail(page, "Last seen")).toHaveText(/^\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$/);
    await expect(detail(page, "Dwell")).toHaveText("1 h 2 min");
    await expect(detail(page, "Range")).toHaveText("0");
    await expect(detail(page, "Vendor")).toHaveText("Stub Vendor");
  });

  test("opens one popup where networks of two layers overlap", async ({ page, api }) => {
    await visitMap(page, { at: "00:00:00:00:00:02", token });
    await waitForNetworkAtCenter(page);
    await canvas(page).click();
    await expect(popup(page)).toHaveCount(1);
    await page.waitForTimeout(500);
    expect(api.networkInfo).toHaveLength(1);
  });

  test("shows markup in an SSID as text", async ({ page }) => {
    await visitMap(page, { at: "00:00:00:00:00:04", token });
    await openPopupAtCenter(page);
    const ssid = '<b>Stub & "Markup"</b><img src=x onerror=window.__xss=1>';
    await expect(popup(page).getByText(ssid, { exact: true })).toBeVisible();
    await expect(popup(page).locator("b, img")).toHaveCount(0);
    expect(await page.evaluate(() => (window as { __xss?: number }).__xss)).toBeUndefined();
  });

  test("draws the popup with the same panel style as the menu", async ({ page }) => {
    await visitMap(page, { token });
    await openPopupAtCenter(page);
    await menuButton(page).click();
    const panelStyle = (el: Element) => {
      const style = getComputedStyle(el);
      return [style.borderRadius, style.padding, style.boxShadow];
    };
    const menuStyle = await page.getByRole("menu").locator("xpath=..").evaluate(panelStyle);
    const popupStyle = await page.locator(".maplibregl-popup-content > div > div").first().evaluate(panelStyle);
    expect(popupStyle).toEqual(menuStyle);
  });

  test("closes the popup with its button and with a click elsewhere on the map", async ({ page }) => {
    await visitMap(page, { token });
    await openPopupAtCenter(page);
    await popup(page).getByRole("button", { name: "Close" }).click();
    await expect(popup(page)).toHaveCount(0);

    await openPopupAtCenter(page);
    await canvas(page).click({ position: { x: 150, y: 650 } });
    await expect(popup(page)).toHaveCount(0);
  });

  test("signs out: removes popup and filter, forgets the token, stops loading networks", async ({ page, api }) => {
    await visitMap(page, { token });
    await openPopupAtCenter(page);
    await menuButton(page).click();
    await page.getByRole("menuitem", { name: "Sign out" }).click();

    await expect(popup(page)).toHaveCount(0);
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
