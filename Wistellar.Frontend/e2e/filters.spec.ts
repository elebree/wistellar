import type { Page } from "@playwright/test";
import { test, expect, stubToken, type StubApi } from "./stub-api";
import {
  canvas,
  enterFilter,
  filterBox,
  filtersButton,
  filtersPane,
  linkFilter,
  menuButton,
  openFilters,
  visitMap,
  waitForNetworkAtCenter,
  waitForNoNetworkAtCenter,
} from "./map";

const defaultFilter = "type=W|B|E|G|C|D|L|N&ssid=_%&time[gt]=1d";

const slider = (page: Page, name: string) => filtersPane(page).getByRole("slider", { name, exact: true });
const summary = (page: Page, label: string) =>
  filtersPane(page).getByRole("group", { name: label, exact: true }).getByTestId("range-summary");
/** A button of the short form, by its accessible name ("Seen over: over 1 h"). */
const shortForm = (page: Page, name: string | RegExp) =>
  page.getByRole("button", { name, exact: typeof name === "string" });
const rangeChips = (page: Page) => shortForm(page, /^(Seen across|Seen over|Locations):/);
const patternField = (page: Page, label: string) =>
  filtersPane(page).getByRole("textbox", { name: label, exact: true });
const nameField = (page: Page) => patternField(page, "Name");
const idField = (page: Page) => patternField(page, "MAC or cell ID");
const patternChips = (page: Page) => shortForm(page, /^(Name|MAC or cell ID):/);

const urlParam = (page: Page, name: string) => new URL(page.url()).searchParams.get(name);
const lastTileParam = (api: StubApi, name: string) => {
  const tile = api.tiles.at(-1);
  return tile ? new URL(tile.url()).searchParams.get(name) : undefined;
};

async function press(page: Page, sliderName: string, key: string, times = 1) {
  for (let i = 0; i < times; i++) await slider(page, sliderName).press(key);
}

test.describe("filters pane", () => {
  test("opens left of the button column, and closes with Escape or on the map", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    const funnel = (await filtersButton(page).boundingBox())!;
    await expect(filtersButton(page)).not.toHaveClass(/active/);
    await expect(rangeChips(page)).toHaveCount(0);

    await filtersButton(page).click();
    await expect(filtersPane(page)).toBeVisible();
    await expect(filtersButton(page)).toHaveAttribute("aria-expanded", "true");
    await expect(filtersPane(page).getByRole("button", { name: "WiFi", exact: true }), "first control").toBeFocused();
    await expect(filterBox(page), "the whole query, at the bottom").toHaveValue(defaultFilter);
    for (const label of ["Seen across", "Seen over", "Locations"]) {
      await expect(summary(page, label), label).toHaveText("Any");
    }
    await expect(filtersPane(page).getByRole("group", { name: "Seen across" })).toHaveAccessibleDescription(
      "Distance between the farthest places it was seen",
    );
    const pane = (await filtersPane(page).boundingBox())!;
    const bar = (await page.locator(".time-filter").boundingBox())!;
    expect(pane.x + pane.width, "ends 10px left of the funnel").toBeCloseTo(funnel.x - 10, 0);
    expect(pane.y + pane.height, "10px above the time range bar").toBeCloseTo(bar.y - 10, 0);
    expect(pane.y, "the top row is empty: 10px from the top or more").toBeGreaterThanOrEqual(10);

    await page.keyboard.press("Escape");
    await expect(filtersPane(page)).toHaveCount(0);
    await expect(filtersButton(page)).toBeFocused();

    await openFilters(page);
    await canvas(page).click({ position: { x: 300, y: 400 } });
    await expect(filtersPane(page)).toHaveCount(0);
  });

  test("writes a moved handle into the filter, the link and the tiles", async ({ page, api }) => {
    await visitMap(page, { token: stubToken() });
    await openFilters(page);

    await press(page, "Minimum distance", "ArrowRight");
    await expect(filterBox(page)).toHaveValue(`${defaultFilter}&range[gt]=10`);
    await expect(summary(page, "Seen across")).toHaveText("over 10 m");
    await expect.poll(() => urlParam(page, "range[gt]")).toBe("10");
    await expect.poll(() => lastTileParam(api, "range[gt]")).toBe("10");

    await press(page, "Maximum distance", "ArrowLeft");
    await expect(filterBox(page)).toHaveValue(`${defaultFilter}&range[gt]=10&range[lt]=10000`);
    await expect(summary(page, "Seen across")).toHaveText("10 m – 10 km");

    // At the open ends a handle sets no bound.
    await press(page, "Minimum distance", "ArrowLeft");
    await press(page, "Maximum distance", "ArrowRight");
    await expect(filterBox(page)).toHaveValue(defaultFilter);
    await expect.poll(() => urlParam(page, "range[gt]")).toBeNull();
  });

  test("writes time spans as durations", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await openFilters(page);

    await press(page, "Minimum time span", "ArrowRight", 4);
    await expect(filterBox(page)).toHaveValue(`${defaultFilter}&dwell[gt]=1h`);
    await expect(summary(page, "Seen over")).toHaveText("over 1 h");
    await expect(slider(page, "Minimum time span")).toHaveAttribute("aria-valuetext", "More than 1 h");

    await press(page, "Minimum time span", "Home");
    await expect(filterBox(page)).toHaveValue(defaultFilter);
  });

  test("counts locations inclusively, for the server's strict bounds", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await openFilters(page);

    // "At most 500" is `[lt]=501`, "at least 2" is `[gt]=1`.
    await press(page, "Maximum locations", "ArrowLeft", 2);
    await expect(filterBox(page)).toHaveValue(`${defaultFilter}&locations[lt]=501`);
    await expect(summary(page, "Locations")).toHaveText("at most 500");
    await press(page, "Minimum locations", "ArrowRight");
    await expect(filterBox(page)).toHaveValue(`${defaultFilter}&locations[lt]=501&locations[gt]=1`);
    await expect(summary(page, "Locations")).toHaveText("2 – 500");
    await expect(slider(page, "Minimum locations")).toHaveAttribute("aria-valuetext", "At least 2");

    // The lower handle at 1 sets no bound: every network has a location.
    await press(page, "Minimum locations", "Home");
    await press(page, "Maximum locations", "End");
    await expect(filterBox(page)).toHaveValue(defaultFilter);
  });

  test("shows the bounds in the link's filter, also ones between the stops", async ({ page }) => {
    await visitMap(page, {
      token: stubToken(),
      filter: "type=W&range[gt]=37&dwell[lt]=2h&locations[gt]=3&locations[lt]=50",
    });
    await openFilters(page);

    await expect(summary(page, "Seen across")).toHaveText("over 37 m");
    await expect(slider(page, "Minimum distance")).toHaveAttribute("aria-valuetext", "More than 37 m");
    await expect(summary(page, "Seen over")).toHaveText("under 2 h");
    await expect(summary(page, "Locations")).toHaveText("4 – 49");
  });

  test("keeps the lower distance handle below the upper one", async ({ page }) => {
    await visitMap(page, { token: stubToken(), filter: "type=W&range[gt]=5000" });
    await openFilters(page);

    await press(page, "Minimum distance", "ArrowRight", 3);
    await expect(filterBox(page)).toHaveValue("type=W&range[gt]=10000");
    await expect(summary(page, "Seen across")).toHaveText("over 10 km");

    await press(page, "Maximum distance", "ArrowLeft");
    await expect(filterBox(page)).toHaveValue("type=W&range[gt]=10000");
  });

  test("lets the locations handles meet, and offers nothing below one location", async ({ page }) => {
    await visitMap(page, { token: stubToken(), filter: "type=W" });
    await openFilters(page);

    // The upper handle all the way left: networks seen once.
    await press(page, "Maximum locations", "Home");
    await expect(filterBox(page)).toHaveValue("type=W&locations[lt]=2");
    await expect(summary(page, "Locations")).toHaveText("at most 1");
    await expect(slider(page, "Maximum locations")).toHaveAttribute("aria-valuetext", "At most 1");

    // Both handles on 5, typed into the query field.
    await filterBox(page).fill("type=W&locations[gt]=4&locations[lt]=6");
    await filterBox(page).press("Enter");
    await expect(summary(page, "Locations")).toHaveText("exactly 5");
  });

  // The network at the center: 3 locations over 1 h 2 min.
  test("hides the networks with fewer locations", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await waitForNetworkAtCenter(page);
    await openFilters(page);

    await press(page, "Minimum locations", "ArrowRight", 3);
    await expect(summary(page, "Locations")).toHaveText("at least 5");
    await waitForNoNetworkAtCenter(page);

    await press(page, "Minimum locations", "ArrowLeft");
    await expect(summary(page, "Locations")).toHaveText("at least 3");
    await waitForNetworkAtCenter(page);
  });

  test("hides the networks seen over a shorter time", async ({ page }) => {
    await visitMap(page, { token: stubToken(), filter: `${defaultFilter}&dwell[gt]=1h` });
    await waitForNetworkAtCenter(page);
    await openFilters(page);

    await press(page, "Minimum time span", "ArrowRight");
    await expect(summary(page, "Seen over")).toHaveText("over 6 h");
    await waitForNoNetworkAtCenter(page);
  });

  test("shows what limits the map in the top row: the shown types and a chip per range", async ({ page }) => {
    await visitMap(page, {
      token: stubToken(),
      filter: "type=W|E&range[gt]=10&range[lt]=1000&dwell[gt]=1h",
    });
    await expect(filtersButton(page)).toHaveClass(/active/);

    const types = shortForm(page, "Network types: WiFi, Bluetooth LE");
    await expect(types.locator(".type-badge")).toHaveCount(2);
    const badge = (await types.locator(".type-badge").first().boundingBox())!;
    expect([badge.width, badge.height], "as large as the other chips' icons").toEqual([20, 20]);
    const across = shortForm(page, "Seen across: 10 m – 1 km");
    const over = shortForm(page, "Seen over: over 1 h");
    await expect(across).toHaveText("10 m – 1 km");
    await expect(over).toHaveText("over 1 h");
    await expect(across.locator("svg")).toBeVisible();
    await expect(shortForm(page, /^Locations:/)).toHaveCount(0);

    expect((await types.boundingBox())!.x, "10px from the left edge").toBeCloseTo(10, 0);
    // Like the map's buttons: as high, the same see-through white and gray edge, rounded ends.
    const lookOf = (locator: typeof types) =>
      locator.evaluate((el) => {
        const style = getComputedStyle(el);
        return { height: style.height, border: style.border, background: style.backgroundColor };
      });
    const buttonLook = await lookOf(menuButton(page));
    for (const item of [types, across, over]) {
      expect((await item.boundingBox())!.y, "in the top row").toBeCloseTo(10, 0);
      expect(await lookOf(item)).toEqual(buttonLook);
      await expect(item).toHaveCSS("border-radius", "18px");
    }

    // Each opens the pane at its control, also while it is open.
    await over.click();
    await expect(slider(page, "Minimum time span")).toBeFocused();
    await types.click();
    await expect(filtersPane(page)).toBeVisible();
    await expect(filtersPane(page).getByRole("button", { name: "WiFi", exact: true })).toBeFocused();
  });

  // The network at the center is "Stub WiFi One", 00:00:00:00:00:01; the default `_%` is any name.
  test("writes a typed name pattern into the filter, the link and the tiles", async ({ page, api }) => {
    await visitMap(page, { token: stubToken() });
    await waitForNetworkAtCenter(page);
    await openFilters(page);
    await expect(nameField(page)).toHaveValue("_%");
    await expect(nameField(page)).toHaveAccessibleDescription(
      "SSID, Bluetooth device name or cell operator % for any text, _ for one character, | between alternatives",
    );
    await expect(patternChips(page), "the default limits nothing").toHaveCount(0);

    // Sent as it is, the `%be` would reach the server as an escaped byte.
    await nameField(page).fill("%beacon%");
    await nameField(page).press("Enter");
    await expect(filterBox(page)).toHaveValue("type=W|B|E|G|C|D|L|N&ssid=%beacon%&time[gt]=1d");
    await expect.poll(() => urlParam(page, "ssid")).toBe("%beacon%");
    await expect.poll(() => lastTileParam(api, "ssid")).toBe("%beacon%");
    await waitForNoNetworkAtCenter(page);

    // Either alternative, in any case.
    await nameField(page).fill("stub wifi one|%beacon%");
    await nameField(page).press("Enter");
    await waitForNetworkAtCenter(page);

    await page.reload();
    await openFilters(page);
    await expect(filterBox(page)).toHaveValue("type=W|B|E|G|C|D|L|N&ssid=stub wifi one|%beacon%&time[gt]=1d");
  });

  test("writes a typed MAC or cell ID pattern into the filter and the tiles", async ({ page, api }) => {
    await visitMap(page, { token: stubToken() });
    await waitForNetworkAtCenter(page);
    await openFilters(page);
    await expect(idField(page), "no pattern: every network").toHaveValue("");
    await expect(idField(page)).toHaveAttribute("placeholder", "Any");
    await expect(idField(page)).toHaveAccessibleDescription(
      "MAC address, or MCCMNC_LAC_CID for a cell % for any text, _ for one character, | between alternatives",
    );

    await idField(page).fill("00:00:00:00:00:02");
    await idField(page).press("Enter");
    await expect(filterBox(page)).toHaveValue(`${defaultFilter}&bssid=00:00:00:00:00:02`);
    await expect.poll(() => lastTileParam(api, "bssid")).toBe("00:00:00:00:00:02");
    await waitForNoNetworkAtCenter(page);

    await idField(page).fill("%:02|%:01");
    await idField(page).press("Enter");
    await waitForNetworkAtCenter(page);
  });

  test("shows the patterns in the top row, each opening the pane at its field", async ({ page }) => {
    await visitMap(page, { token: stubToken(), filter: "type=W&ssid=%Overlap%&bssid=00:00:00:00:00:0_" });
    await expect(filtersButton(page)).toHaveClass(/active/);
    const name = shortForm(page, "Name: %Overlap%");
    const id = shortForm(page, "MAC or cell ID: 00:00:00:00:00:0_");
    await expect(name).toHaveText("%Overlap%");
    await expect(id).toHaveText("00:00:00:00:00:0_");
    expect((await name.boundingBox())!.y, "in the top row").toBeCloseTo(10, 0);

    await name.click();
    await expect(nameField(page)).toBeFocused();
    await expect(nameField(page)).toHaveValue("%Overlap%");
    await page.keyboard.press("Escape");
    await id.click();
    await expect(idField(page)).toBeFocused();
    await expect(idField(page)).toHaveValue("00:00:00:00:00:0_");
  });

  test("takes a typed pattern when the pane closes on the map, and drops it on Escape", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await openFilters(page);
    await nameField(page).fill("Stub%");
    await page.keyboard.press("Escape");
    await expect(filtersPane(page)).toHaveCount(0);
    await expect(patternChips(page), "nothing taken").toHaveCount(0);

    await openFilters(page);
    await expect(nameField(page)).toHaveValue("_%");
    await nameField(page).fill("Stub%");
    await canvas(page).click({ position: { x: 300, y: 400 } });
    await expect(filtersPane(page)).toHaveCount(0);
    await expect.poll(() => linkFilter(page)).toBe("type=W|B|E|G|C|D|L|N&ssid=Stub%&time[gt]=1d");
    await expect(shortForm(page, "Name: Stub%")).toBeVisible();

    // Emptied, it takes `ssid` out: networks with and without a name.
    await openFilters(page);
    await nameField(page).fill("");
    await nameField(page).press("Enter");
    await expect(filterBox(page)).toHaveValue("type=W|B|E|G|C|D|L|N&time[gt]=1d");
    await expect(patternChips(page)).toHaveCount(0);
  });

  test("takes a whole query typed into the query field, also when the pane closes on the map", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await enterFilter(page, "type=W&dwell[gt]=1h");
    await expect(summary(page, "Seen over"), "the controls follow").toHaveText("over 1 h");
    await expect(shortForm(page, "Seen over: over 1 h")).toBeVisible();
    await expect.poll(() => linkFilter(page)).toBe("type=W&dwell[gt]=1h");

    // Escape drops what was typed.
    await filterBox(page).fill("type=W");
    await page.keyboard.press("Escape");
    await expect(filtersPane(page)).toHaveCount(0);
    await expect.poll(() => linkFilter(page)).toBe("type=W&dwell[gt]=1h");

    await openFilters(page);
    await filterBox(page).fill("type=W|L");
    await canvas(page).click({ position: { x: 300, y: 400 } });
    await expect(filtersPane(page)).toHaveCount(0);
    await expect.poll(() => linkFilter(page)).toBe("type=W|L");
    await expect(shortForm(page, /^Seen over:/)).toHaveCount(0);
  });

  test("resets the types, the patterns and all bounds at once", async ({ page }) => {
    await visitMap(page, {
      token: stubToken(),
      filter: "type=W&ssid=Stub%&bssid=00:%&range[gt]=10&dwell[gt]=1h",
    });
    await openFilters(page);
    await filtersPane(page).getByRole("button", { name: "Reset" }).click();

    // The name back to the default, any name; no MAC or cell ID pattern.
    await expect(filterBox(page)).toHaveValue("type=W|B|E|G|C|D|L|N&ssid=_%");
    await expect(nameField(page)).toHaveValue("_%");
    await expect(idField(page)).toHaveValue("");
    await expect(filtersButton(page)).not.toHaveClass(/active/);
    await expect(rangeChips(page)).toHaveCount(0);
    await expect(patternChips(page)).toHaveCount(0);
    await expect(shortForm(page, /^Network types:/)).toHaveCount(0);
    await expect(filtersPane(page).getByRole("button", { name: "Reset" })).toHaveCount(0);
  });

  test("aren't there while signed out", async ({ page }) => {
    await visitMap(page);
    await expect(filtersButton(page)).toHaveCount(0);
  });
});

test.describe("filters on a phone", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("fit the pane in the window", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await openFilters(page);
    const pane = (await filtersPane(page).boundingBox())!;
    const funnel = (await filtersButton(page).boundingBox())!;
    expect(pane.x).toBeGreaterThanOrEqual(10);
    expect(pane.x + pane.width, "left of the button column").toBeLessThanOrEqual(funnel.x - 10);
  });

  test("wrap the short form onto more lines", async ({ page }) => {
    // Bounds the network at the center passes: WiFi, 0 m across, 3 locations over 1 h 2 min.
    await visitMap(page, {
      token: stubToken(),
      filter: "type=W|L&range[lt]=1000&dwell[gt]=1h&locations[lt]=50",
    });
    await expect(rangeChips(page)).toHaveCount(3);
    const first = (await shortForm(page, /^Network types:/).boundingBox())!;
    const menu = (await menuButton(page).boundingBox())!;
    let wrapped = false;
    for (const box of await Promise.all((await rangeChips(page).all()).map((c) => c.boundingBox()))) {
      expect(box!.x + box!.width, "ends 10px before the menu button").toBeLessThanOrEqual(menu.x - 10);
      wrapped ||= box!.y > first.y + first.height;
    }
    expect(wrapped, "some chips on a line of their own").toBe(true);
    // The network details take the place of the whole filter row: see layout.spec.ts.
  });
});
