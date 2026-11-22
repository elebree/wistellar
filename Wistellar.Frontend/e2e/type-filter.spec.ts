import type { Page, Route } from "@playwright/test";
import { test, expect, isTile, positionOf, stubToken, type StubApi } from "./stub-api";
import {
  canvas,
  filtersButton,
  openFilters,
  typeToggles,
  visitMap,
  waitForNetworkAtCenter,
  waitForNoNetworkAtCenter,
} from "./map";

// The type toggles, in the filters pane, read and write the filter's `type`.

const filterBox = (page: Page) => page.locator("#filter");
const toggle = (page: Page, label: string) => typeToggles(page).getByRole("button", { name: label, exact: true });

// Each toggle is named by its badge text.
const labels = ["WiFi", "BT", "BLE", "GSM", "CDMA", "3G", "4G", "5G"];
const defaultFilter = "type=W|B|E|G|C|D|L|N&ssid=_%&time[gt]=1d";

/** Opens the map on a stub WiFi network with a filter in the link, signed in, and the filters pane. */
async function visitWithFilter(page: Page, filter: string) {
  await page.addInitScript((t) => localStorage.setItem("access_token", t), stubToken());
  const [lon, lat] = positionOf("00:00:00:00:00:01");
  await page.goto(`/?z=14&lat=${lat}&lon=${lon}&${filter}`);
  await expect(canvas(page)).toBeVisible();
  await openFilters(page);
}

const urlParam = (page: Page, name: string) => new URL(page.url()).searchParams.get(name);
const lastTileParam = (api: StubApi, name: string) => {
  const tile = api.tiles.at(-1);
  return tile ? new URL(tile.url()).searchParams.get(name) : undefined;
};

async function expectPressed(page: Page, pressed: string[]) {
  for (const label of labels) {
    await expect(toggle(page, label), label).toHaveAttribute("aria-pressed", String(pressed.includes(label)));
  }
}

test.describe("network type toggles", () => {
  test("show the types in the link's filter as pressed badges, in the color of their layer", async ({ page }) => {
    await visitWithFilter(page, "type=W|L&ssid=_%");

    await expectPressed(page, ["WiFi", "4G"]);
    await expect(toggle(page, "WiFi")).toHaveAttribute("style", /--type-color: tomato/);
    await expect(toggle(page, "4G")).toHaveAttribute("style", /--type-color: darkolivegreen/);
    await expect(toggle(page, "WiFi")).not.toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
    await expect(toggle(page, "BT")).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
    await expect(toggle(page, "5G")).toHaveAttribute("title", "5G NR");
    await expect(toggle(page, "4G")).toHaveAttribute("title", "LTE (4G)");

    // Icons in squares of one size.
    for (const label of labels) {
      const badge = toggle(page, label);
      const box = (await badge.boundingBox())!;
      expect([box.width, box.height], label).toEqual([32, 32]);
      const icon = (await badge.locator("svg").boundingBox())!;
      expect([icon.width, icon.height], `${label} icon`).toEqual([32, 32]);
    }
    await expect(toggle(page, "5G")).toHaveCSS("border-radius", "4px");
  });

  test("show every type as pressed when the filter lists none", async ({ page }) => {
    await visitWithFilter(page, "ssid=_%");
    await expectPressed(page, labels);
  });

  test("switch a type off and on again in the filter, the link and the tiles", async ({ page, api }) => {
    await visitMap(page, { token: stubToken() });
    await expect(filterBox(page)).toHaveValue(defaultFilter);
    await openFilters(page);
    await expectPressed(page, labels);

    await toggle(page, "BLE").click();

    await expect(filterBox(page)).toHaveValue("type=W|B|G|C|D|L|N&ssid=_%&time[gt]=1d");
    await expectPressed(page, labels.filter((label) => label !== "BLE"));
    await expect.poll(() => urlParam(page, "type")).toBe("W|B|G|C|D|L|N");
    await expect.poll(() => lastTileParam(api, "type")).toBe("W|B|G|C|D|L|N");

    await toggle(page, "BLE").click();

    await expect(filterBox(page)).toHaveValue(defaultFilter);
    await expectPressed(page, labels);
    await expect.poll(() => urlParam(page, "type")).toBe("W|B|E|G|C|D|L|N");
    await expect.poll(() => lastTileParam(api, "type")).toBe("W|B|E|G|C|D|L|N");
  });

  test("hide the networks of a type that is switched off", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await waitForNetworkAtCenter(page);
    await openFilters(page);

    await toggle(page, "WiFi").click();
    await waitForNoNetworkAtCenter(page);

    await toggle(page, "WiFi").click();
    await waitForNetworkAtCenter(page);
  });

  test("follow types typed into the filter box", async ({ page }) => {
    await visitMap(page, { token: stubToken() });

    await filterBox(page).fill("type=E|G&ssid=_%");
    await filterBox(page).press("Enter");
    await openFilters(page);

    await expectPressed(page, ["BLE", "GSM"]);
  });

  test("keep the last type on, since an empty list would show every type", async ({ page }) => {
    await visitWithFilter(page, "type=E&ssid=_%");
    await expect(toggle(page, "BLE")).toHaveAttribute("aria-disabled", "true");
    await expect(toggle(page, "WiFi")).not.toHaveAttribute("aria-disabled");

    // Playwright waits for an aria-disabled button to become enabled; a user can still click it.
    await toggle(page, "BLE").click({ force: true });

    await expect(filterBox(page)).toHaveValue("type=E&ssid=_%");
    await expectPressed(page, ["BLE"]);
  });

  test("keep types they have no toggle for", async ({ page }) => {
    await visitWithFilter(page, "type=F|W&ssid=_%");
    await expectPressed(page, ["WiFi"]);

    await toggle(page, "4G").click();
    await expect(filterBox(page)).toHaveValue("type=W|L|F&ssid=_%");

    // With NFC still listed, WiFi isn't the last type and can go.
    await toggle(page, "WiFi").click();
    await toggle(page, "4G").click();
    await expect(filterBox(page)).toHaveValue("type=F&ssid=_%");
    await expectPressed(page, []);
  });

  test("aren't there while signed out", async ({ page }) => {
    await visitMap(page);
    await expect(filtersButton(page)).toHaveCount(0);
  });
});

test.describe("tiles on a filter change", () => {
  const typeOf = (url: string) => new URL(url).searchParams.get("type");

  /** From now on, the stub server holds every tile request until the test answers it or releases them. */
  async function holdTiles(page: Page) {
    const held: Route[] = [];
    const hold = (route: Route) => {
      held.push(route);
    };
    await page.route(isTile, hold);
    return { held, release: () => page.unroute(isTile, hold) };
  }

  test("keep the networks on the map until the new filter's tiles are there", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await waitForNetworkAtCenter(page);
    await openFilters(page);
    const { held } = await holdTiles(page);

    // WiFi stays shown, so the WiFi network at the center does too.
    await toggle(page, "BLE").click();
    await expect.poll(() => held.length).toBeGreaterThan(0);
    await waitForNetworkAtCenter(page);
  });

  test("cancel the requests for a filter changed again before they were answered", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await waitForNetworkAtCenter(page);
    await openFilters(page);
    const { held, release } = await holdTiles(page);
    const failed = new Set<string>();
    page.on("requestfailed", (request) => {
      if (isTile(new URL(request.url()))) failed.add(request.url());
    });

    await toggle(page, "WiFi").click();
    await expect.poll(() => held.length).toBeGreaterThan(0);
    await toggle(page, "BT").click();

    const forFilter = (types: string) => held.filter((route) => typeOf(route.request().url()) === types);
    const superseded = forFilter("B|E|G|C|D|L|N");
    expect(superseded.length).toBeGreaterThan(0);
    await expect
      .poll(() => superseded.filter((route) => !failed.has(route.request().url())).length, "still running")
      .toBe(0);

    // The current filter's tiles, answered, take the WiFi network away.
    await expect.poll(() => forFilter("E|G|C|D|L|N").length).toBeGreaterThan(0);
    await release();
    for (const route of forFilter("E|G|C|D|L|N")) await route.fallback();
    await waitForNoNetworkAtCenter(page);
  });
});
