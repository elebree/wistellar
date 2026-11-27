import type { Page } from "@playwright/test";
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
    // The type as the same icon as its toggle, without the toggle's box, as large as other icons.
    const type = panel(page).getByRole("img", { name: "WiFi", exact: true });
    await expect(type).toHaveClass(/type-badge/);
    await expect(type).toHaveAttribute("style", /--type-color: tomato/);
    await expect(type).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
    await openFilters(page);
    const toggleColor = await typeToggles(page)
      .getByRole("button", { name: "WiFi", exact: true })
      .evaluate((el) => getComputedStyle(el).color);
    await expect(type, "the WiFi toggle's color").toHaveCSS("color", toggleColor);
    const box = (await type.boundingBox())!;
    expect([box.width, box.height], "as large as the other icons").toEqual([20, 20]);
    await expect(panel(page).getByRole("button", { name: /^Copy capability: / })).toHaveText(["WPA2-PSK-CCMP", "ESS"]);
    await expect(detail(page, "Last seen")).toHaveText(/^\d{2}\.\d{2}\.\d{4}, \d{2}:\d{2}:\d{2}$/);
    await expect(detail(page, "Seen over")).toHaveText("1 h 2 min");
    await expect(detail(page, "Seen across")).toHaveText("0 m");
    await expect(detail(page, "Locations")).toHaveText("3");
    await expect(detail(page, "Level")).toHaveText("-50 dBm");
    await expect(detail(page, "Vendor")).toHaveText("Stub Vendor");
  });

  test("shows the type, the name and the MAC in one line, and an icon by each detail", async ({ page }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    const line = await Promise.all(
      [
        panel(page).getByRole("img", { name: "WiFi", exact: true }),
        panel(page).getByRole("button", { name: "Copy name: Stub WiFi One" }),
        panel(page).getByRole("button", { name: "Copy MAC or cell ID: 00:00:00:00:00:01" }),
      ].map(async (item) => (await item.boundingBox())!),
    );
    for (const [i, box] of line.entries()) {
      expect(box.y + box.height / 2, `item ${i} in the line`).toBeCloseTo(line[0].y + line[0].height / 2, 0);
      if (i > 0) expect(box.x, `item ${i} after the one before`).toBeGreaterThan(line[i - 1].x + line[i - 1].width);
    }

    // No icons in the chips; every detail with one, the filter pane's where it is one of its filters.
    await expect(panel(page).getByRole("button", { name: /^Copy / }).locator(".icon")).toHaveCount(0);
    const detailIcons = await panel(page).locator("dt .icon").count();
    expect(detailIcons, "an icon by every detail").toBe(await panel(page).locator("dt").count());
    const iconOf = (locator: ReturnType<typeof panel>) => locator.locator(".icon").first().innerHTML();
    const acrossIcon = await iconOf(panel(page).locator("dt", { hasText: "Seen across" }));
    await openFilters(page);
    expect(acrossIcon).toBe(await iconOf(page.locator('[data-range="range"]')));
  });

  /** The values of the details, top to bottom as they are listed. */
  const detailOrder = ["First seen", "Last seen", "Seen over", "Seen across", "Locations", "Level", "Vendor"];
  const valueBoxes = (page: Page) =>
    Promise.all(detailOrder.map(async (label) => (await detail(page, label).boundingBox())!));

  test("lists the details one to a row beside the map, values lined up", async ({ page }) => {
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    const labels = (await panel(page).locator("dt").allTextContents()).map((label) => label.trim());
    expect(labels).toEqual(detailOrder);
    // No first time from the server: the last one less the time it was seen over (3725 s).
    const firstSeen = await page.evaluate(() => new Date(1792885194000 - 3725 * 1000).toLocaleString("de-DE"));
    await expect(detail(page, "First seen")).toHaveText(firstSeen);

    const values = await valueBoxes(page);
    for (const [i, box] of values.entries()) {
      expect(box.x, `value ${i} lined up`).toBeCloseTo(values[0].x, 0);
      if (i > 0) expect(box.y, `value ${i} below the one before`).toBeGreaterThan(values[i - 1].y);
    }
  });

  for (const { width, two } of [
    { width: 390, two: false },
    { width: 600, two: true },
  ]) {
    test(`lists the details ${two ? "two" : "one"} to a row across a ${width}px phone`, async ({ page }) => {
      await page.setViewportSize({ width, height: 844 });
      await visitMap(page, { token });
      await openPanelAtCenter(page);
      expect((await panel(page).boundingBox())!.width, "full width").toBe(width);
      const values = await valueBoxes(page);
      if (!two) {
        for (const [i, box] of values.entries()) {
          expect(box.x, `value ${i} lined up`).toBeCloseTo(values[0].x, 0);
          if (i > 0) expect(box.y, `value ${i} below the one before`).toBeGreaterThan(values[i - 1].y);
        }
        return;
      }
      // First and last seen, seen over and across, locations and level side by side; the vendor below.
      for (const i of [0, 2, 4]) {
        const [left, right] = [values[i], values[i + 1]];
        expect(right.y, `${detailOrder[i + 1]} beside ${detailOrder[i]}`).toBeCloseTo(left.y, 0);
        expect(right.x, `${detailOrder[i + 1]} right of ${detailOrder[i]}`).toBeGreaterThan(left.x + left.width);
      }
      expect(values[6].x, "the vendor's value under the left values").toBeCloseTo(values[0].x, 0);
      expect(values[6].y, "the vendor below").toBeGreaterThan(values[5].y);
    });
  }

  test("copies the name, the MAC or a capability with a click, and says so", async ({ page, context }) => {
    await context.grantPermissions(["clipboard-read", "clipboard-write"]);
    await visitMap(page, { token });
    await openPanelAtCenter(page);
    for (const [name, value] of [
      ["Copy name: Stub WiFi One", "Stub WiFi One"],
      ["Copy MAC or cell ID: 00:00:00:00:00:01", "00:00:00:00:00:01"],
      ["Copy capability: ESS", "ESS"],
    ]) {
      await panel(page).getByRole("button", { name }).click();
      await expect.poll(() => page.evaluate(() => navigator.clipboard.readText())).toBe(value);
      await expect(panel(page).getByRole("status")).toHaveText("Copied");
    }
  });

  test("shows the details on the left under the short form, not at the clicked point", async ({ page }) => {
    await visitMap(page, { token, filter: "type=W" });
    await openPanelAtCenter(page);
    // Only phones give the top row's place to the details.
    const types = page.getByRole("button", { name: "Network types: WiFi", exact: true });
    await expect(types).toBeVisible();
    await expect(filtersButton(page)).toBeVisible();
    const row = (await types.boundingBox())!;
    const box = (await panel(page).boundingBox())!;
    expect(Math.abs(box.x - row.x), "left edge lines up with the short form").toBeLessThan(1);
    expect(box.y - (row.y + row.height), "10px below the short form").toBeCloseTo(10, 0);
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
    await expect(filtersButton(page)).toHaveCount(0);
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
    await expect(filtersButton(page)).toHaveCount(0);

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
