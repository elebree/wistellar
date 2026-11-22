import type { Page } from "@playwright/test";
import { test, expect, positionOf, stubToken, type StubApi } from "./stub-api";
import { canvas, visitMap } from "./map";

// The time range bar under the map reads and writes the filter's `time[gt]` and `time[lt]`.

const filterBox = (page: Page) => page.locator("#filter");
const timeline = (page: Page) => page.getByRole("group", { name: "Timeline" });
const startHandle = (page: Page) => page.getByRole("slider", { name: "Start", exact: true });
const endHandle = (page: Page) => page.getByRole("slider", { name: "End", exact: true });

const april = { from: "2026-04-01T00:00:00Z", to: "2026-05-01T00:00:00Z" };

/** Opens the map with a filter in the link, signed in. */
async function visitWithFilter(page: Page, filter: string) {
  await page.addInitScript((t) => localStorage.setItem("access_token", t), stubToken());
  const [lon, lat] = positionOf("00:00:00:00:00:01");
  await page.goto(`/?z=14&lat=${lat}&lon=${lon}&${filter}`);
  await expect(canvas(page)).toBeVisible();
}

const urlParam = (page: Page, name: string) => new URL(page.url()).searchParams.get(name);
const lastTileParam = (api: StubApi, name: string) => {
  const tile = api.tiles.at(-1);
  return tile ? new URL(tile.url()).searchParams.get(name) : undefined;
};

test.use({ timezoneId: "UTC", locale: "en-US" });

test.describe("time range", () => {
  test("shows the range in the link's filter", async ({ page }) => {
    await visitWithFilter(page, `type=W&time[gt]=${april.from}&time[lt]=${april.to}`);

    await expect(startHandle(page)).toHaveAttribute("aria-valuenow", String(Date.parse(april.from)));
    await expect(endHandle(page)).toHaveAttribute("aria-valuenow", String(Date.parse(april.to)));
  });

  test("follows a range typed into the filter box", async ({ page }) => {
    await visitMap(page, { token: stubToken() });

    await filterBox(page).fill(`type=W&time[gt]=${april.from}&time[lt]=${april.to}`);
    await filterBox(page).press("Enter");

    await expect(startHandle(page)).toHaveAttribute("aria-valuenow", String(Date.parse(april.from)));
    await expect(endHandle(page)).toHaveAttribute("aria-valuenow", String(Date.parse(april.to)));
  });

  test("writes a moved range into the filter, the link and the tiles", async ({ page, api }) => {
    await visitWithFilter(page, `type=W&time[gt]=${april.from}&time[lt]=${april.to}`);
    await expect(startHandle(page)).toHaveAttribute("aria-valuenow", String(Date.parse(april.from)));

    await startHandle(page).press("ArrowRight");

    await expect(filterBox(page)).toHaveValue(/^type=W&time\[gt\]=2026-04-\d\dT\d\d:\d\d:\d\dZ&time\[lt\]=2026-05-01T00:00:00Z$/);
    const from = (await filterBox(page).inputValue()).match(/time\[gt\]=([^&]+)/)![1];
    expect(Date.parse(from)).toBeGreaterThan(Date.parse(april.from));
    await expect.poll(() => urlParam(page, "time[gt]")).toBe(from);
    await expect.poll(() => urlParam(page, "time[lt]")).toBe(april.to);
    await expect.poll(() => lastTileParam(api, "time[gt]")).toBe(from);
  });

  test("keeps a range that ends now relative, so it still ends now on the next start", async ({ page, api }) => {
    // The default filter is the last day; the period buttons then pick other "last …" windows.
    await visitMap(page, { token: stubToken() });
    await expect(filterBox(page)).toHaveValue(/&time\[gt\]=1d$/);

    await page.getByRole("button", { name: "Month", exact: true }).click();

    await expect(filterBox(page)).toHaveValue("type=W|B|E|G|C|D|L|N&ssid=_%&time[gt]=30d");
    await expect.poll(() => urlParam(page, "time[gt]")).toBe("30d");
    await expect.poll(() => lastTileParam(api, "time[gt]")).toBe("30d");

    // A fixed range before it leaves no `time[lt]` behind in the link.
    await filterBox(page).fill(`type=W&time[gt]=${april.from}&time[lt]=${april.to}`);
    await filterBox(page).press("Enter");
    await expect.poll(() => urlParam(page, "time[lt]")).toBe(april.to);
    await filterBox(page).fill("type=W&time[gt]=1d");
    await filterBox(page).press("Enter");
    await expect.poll(() => urlParam(page, "time[gt]")).toBe("1d");
    expect(urlParam(page, "time[lt]")).toBeNull();

    await page.goto("/");
    await expect(filterBox(page)).toHaveValue("type=W&time[gt]=1d");
  });

  test("isn't there while signed out", async ({ page }) => {
    await visitMap(page);
    await expect(page.locator(".time-filter")).toHaveCount(0);
  });

  test("lies over the bottom of the map, blurred, with the map's controls above it", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await expect(timeline(page)).toBeVisible();

    const map = (await canvas(page).boundingBox())!;
    const bar = (await page.locator(".time-filter").boundingBox())!;
    expect(map.height, "map fills the window").toBe(800);
    expect(bar.y + bar.height, "bar at the bottom of the window").toBeCloseTo(800, 0);
    await expect(page.locator(".time-filter")).toHaveCSS("background-color", "rgba(0, 0, 0, 0)");
    await expect(page.locator(".time-filter .trs")).toHaveCSS("backdrop-filter", "saturate(1.8) blur(20px)");

    for (const control of [
      page.locator(".maplibregl-ctrl-bottom-right"),
      page.getByRole("link", { name: "Wistellar logo" }),
    ]) {
      const box = (await control.boundingBox())!;
      expect(box.y + box.height, "control above the bar").toBeLessThanOrEqual(bar.y);
    }
    await page.locator(".maplibregl-ctrl-bottom-right").getByRole("button", { name: "Zoom in" }).click();
    await expect.poll(() => new URL(page.url()).searchParams.get("z")).toBe("15.0");
  });

  test("closes its timeline on demand only, and starts as it was left", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await expect(timeline(page), "open by default").toBeVisible();

    // Working on the map leaves it open.
    const map = (await canvas(page).boundingBox())!;
    await page.mouse.click(map.x + map.width / 2, map.y + 100);
    await expect(timeline(page)).toBeVisible();

    await page.locator(".time-filter").getByRole("button", { expanded: true }).click();
    await expect(timeline(page)).toBeHidden();
    await page.reload();
    await expect(page.locator(".time-filter")).toBeVisible();
    await expect(timeline(page), "still closed after a restart").toBeHidden();

    await page.locator(".time-filter").getByRole("button", { expanded: false }).click();
    await expect(timeline(page)).toBeVisible();
    await page.reload();
    await expect(timeline(page), "open again after a restart").toBeVisible();
  });
});

test.describe("time range on a phone", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("shows only its toolbar until the timeline is opened", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await expect(page.locator(".time-filter")).toBeVisible();
    await expect(timeline(page)).toBeHidden();

    // A sheet across the whole width at the bottom edge, with only a top border.
    const card = page.locator(".time-filter .trs");
    const sheet = (await card.boundingBox())!;
    expect(sheet.x, "sheet starts at the left edge").toBe(0);
    expect(sheet.width, "sheet spans the window").toBe(390);
    expect(sheet.y + sheet.height, "sheet at the bottom edge").toBeCloseTo(844, 0);
    await expect(card).toHaveCSS("border-width", "1px 0px 0px");
    await expect(card).toHaveCSS("border-radius", "12px 12px 0px 0px");

    await page.locator(".time-filter").getByRole("button", { expanded: false }).click();

    await expect(timeline(page)).toBeVisible();

    // The bar grew upwards, and the map's controls moved up with it.
    const bar = (await page.locator(".time-filter").boundingBox())!;
    const controls = (await page.locator(".maplibregl-ctrl-bottom-right").boundingBox())!;
    expect(controls.y + controls.height, "controls above the opened bar").toBeLessThanOrEqual(bar.y);
  });

  test("shows the presets over the map in frosted glass", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await page.locator(".time-filter .trs-period-picker").click();

    const presets = page.getByRole("dialog", { name: "Period" });
    await expect(presets).toBeVisible();
    await expect(presets).toHaveCSS("backdrop-filter", "saturate(1.8) blur(20px)");
    await expect(presets).toHaveCSS("background-color", "rgba(255, 255, 255, 0.72)");
  });
});
