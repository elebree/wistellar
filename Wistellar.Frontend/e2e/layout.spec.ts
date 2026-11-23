import type { Locator, Page } from "@playwright/test";
import { test, expect, stubToken } from "./stub-api";
import { style } from "./stub-data";
import { filtersButton, locateButton, menuButton, openPanelAtCenter, panel, visitMap } from "./map";

/** Whether the element is what a tap at its center would hit, i.e. nothing covers it. */
const isOnTop = (locator: Locator) =>
  locator.evaluate((el) => {
    const r = el.getBoundingClientRect();
    const hit = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
    return hit !== null && el.contains(hit);
  });

/** The short form of the filter, in the top row. */
const shortForm = (page: Page) => page.locator("[data-opens-filters] button");

test.describe("phone layout", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("keeps the short form and the menu button side by side in the top row", async ({ page }) => {
    await visitMap(page, { token: stubToken(), filter: "type=W|L&range[lt]=1000&dwell[gt]=1h&locations[lt]=50" });
    await expect(shortForm(page)).toHaveCount(4);
    const first = (await shortForm(page).first().boundingBox())!;
    const button = (await menuButton(page).boundingBox())!;
    expect(first.x, "10px from the left edge").toBeCloseTo(10, 0);
    expect(Math.abs(first.y - button.y), "same top").toBeLessThan(1);
    expect(Math.abs(first.height - button.height), "same height").toBeLessThan(1);
    expect(Math.abs(button.x + button.width - (390 - 10)), "10px from the right edge").toBeLessThan(1);
    for (const box of await Promise.all((await shortForm(page).all()).map((chip) => chip.boundingBox()))) {
      expect(box!.x + box!.width, "ends 10px before the menu button").toBeLessThanOrEqual(button.x - 10);
    }
  });

  test("puts the funnel at the right edge, over the zoom buttons", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    const funnel = (await filtersButton(page).boundingBox())!;
    const zoomIn = (await page.locator(".maplibregl-ctrl-zoom-in").boundingBox())!;
    expect(funnel.x + funnel.width, "10px from the right edge").toBeCloseTo(390 - 10, 0);
    expect(zoomIn.y - (funnel.y + funnel.height), "10px above the zoom buttons").toBeCloseTo(10, 0);
    expect(await isOnTop(filtersButton(page)), "funnel not covered").toBe(true);
  });

  test("shows network details full width across the top, in place of the short form", async ({ page }) => {
    await visitMap(page, { token: stubToken(), filter: "type=W" });
    await expect(shortForm(page)).toBeVisible();
    await openPanelAtCenter(page);
    const box = (await panel(page).boundingBox())!;
    expect(box.x, "flush left").toBe(0);
    expect(box.y, "flush with the top").toBe(0);
    expect(box.width, "full width").toBe(390);
    await expect(panel(page), "square corners").toHaveCSS("border-radius", "0px");

    await expect(shortForm(page), "no short form while the details are open").toBeHidden();
    expect(await isOnTop(filtersButton(page)), "the funnel stays usable, below the panel").toBe(true);

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

  test("brings the short form back when the details close", async ({ page }) => {
    await visitMap(page, { token: stubToken(), filter: "type=W" });
    await openPanelAtCenter(page);
    await expect(shortForm(page)).toBeHidden();
    await panel(page).getByRole("button", { name: "Close" }).click();
    await expect(panel(page)).toHaveCount(0);
    await expect(shortForm(page)).toBeVisible();
    await expect(filtersButton(page)).toBeVisible();
    expect(await isOnTop(shortForm(page)), "short form usable again").toBe(true);
  });
});

test.describe("buttons down the right edge", () => {
  // The time range bar has a "Zoom in" button too, so these go by MapLibre's classes.
  const zoomIn = (page: Page) => page.locator(".maplibregl-ctrl-zoom-in");
  const zoomOut = (page: Page) => page.locator(".maplibregl-ctrl-zoom-out");
  const compass = (page: Page) => page.locator(".maplibregl-ctrl-compass");
  // Top to bottom.
  const buttons = (page: Page) => [filtersButton(page), zoomIn(page), zoomOut(page), locateButton(page), compass(page)];
  const look = ["width", "height", "border", "background-color", "color"];

  const styleOf = (button: Locator, names: string[]) =>
    button.evaluate(
      (el, names) => {
        const style = getComputedStyle(el);
        return Object.fromEntries(names.map((name) => [name, style.getPropertyValue(name)]));
      },
      names,
    );

  test("stack the funnel and the map's buttons in one column, 10px apart, zoom in and out joined", async ({
    page,
  }) => {
    await visitMap(page, { token: stubToken() });
    const boxes = await Promise.all(buttons(page).map((button) => button.boundingBox()));
    const gaps = [10, -1, 10, 10];
    for (const [i, box] of boxes.entries()) {
      expect(box!.x + box!.width, `button ${i} 10px from the right edge`).toBeCloseTo(1280 - 10, 0);
      if (i > 0) {
        const above = boxes[i - 1]!;
        expect(box!.y - (above.y + above.height), `gap above button ${i}`).toBeCloseTo(gaps[i - 1], 0);
      }
    }
  });

  test("look alike, and like the menu button: round, see-through white, a 1px edge and a 20px icon", async ({
    page,
  }) => {
    await visitMap(page, { token: stubToken() });
    const funnel = await styleOf(filtersButton(page), [...look, "border-radius"]);
    expect(funnel).toMatchObject({ width: "36px", height: "36px", "border-radius": "50%" });
    expect(funnel["background-color"], "slightly see-through white").toMatch(/^rgba\(255, 255, 255, 0\.\d+\)$/);

    for (const button of [menuButton(page), locateButton(page), compass(page)]) {
      expect(await styleOf(button, [...look, "border-radius"])).toEqual(funnel);
    }
    // One capsule: the two halves round off its ends.
    expect(await styleOf(zoomIn(page), look)).toEqual(await styleOf(filtersButton(page), look));
    expect(await styleOf(zoomOut(page), look)).toEqual(await styleOf(filtersButton(page), look));
    await expect(zoomIn(page)).toHaveCSS("border-radius", "18px 18px 0px 0px");
    await expect(zoomOut(page)).toHaveCSS("border-radius", "0px 0px 18px 18px");

    for (const button of buttons(page).slice(1)) {
      const iconSize = await button.locator(".maplibregl-ctrl-icon").evaluate((el) => getComputedStyle(el).maskSize);
      expect(iconSize, "a 20px icon, like the funnel's").toMatch(/^20px\b/);
    }
  });

  test("tint the funnel while the filter limits the map, also under the pointer", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    const plain = await filtersButton(page).evaluate((el) => getComputedStyle(el).backgroundColor);
    await visitMap(page, { token: stubToken(), filter: "type=W" });
    await expect(filtersButton(page)).toHaveClass(/active/);
    const tint = await filtersButton(page).evaluate((el) => getComputedStyle(el).backgroundColor);
    expect(tint).not.toBe(plain);
    await filtersButton(page).hover();
    await expect(filtersButton(page)).toHaveCSS("background-color", tint);
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
