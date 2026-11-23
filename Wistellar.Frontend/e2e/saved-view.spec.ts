import type { Page } from "@playwright/test";
import { test, expect, positionOf, stubToken } from "./stub-api";
import { enterFilter, filterBox, linkFilter, openFilters, visitMap, zoomOut } from "./map";

// The installed app always starts at the manifest's start_url, which has no view or filter in it.

/** The view as the app writes it into the URL. `z` stays a string: the app formats it as "13.0". */
function view(page: Page) {
  const params = new URL(page.url()).searchParams;
  return { z: params.get("z"), lat: Number(params.get("lat")), lon: Number(params.get("lon")) };
}

test.describe("saved view", () => {
  test.beforeEach(async ({ page }) => {
    await visitMap(page, { token: stubToken() });
  });

  test("starts where the map was left, with the same filter", async ({ page }) => {
    await enterFilter(page, "type=W&ssid=Stub%");
    await zoomOut(page);
    await expect.poll(() => view(page).z).toBe("13.0");
    const left = view(page);

    await page.goto("/");

    await expect.poll(() => view(page)).toEqual(left);
    await openFilters(page);
    await expect(filterBox(page)).toHaveValue("type=W&ssid=Stub%");
  });

  test("leaves a link's view and filter alone", async ({ page }) => {
    await zoomOut(page);
    await expect.poll(() => view(page).z).toBe("13.0");

    const [lon, lat] = positionOf("00:00:00:00:00:02");
    await page.goto(`/?z=12&lat=${lat}&lon=${lon}&type=W`);

    // The app rewrites the view into the URL from the map, so "12.0" means the map is at the link.
    await expect.poll(() => view(page)).toEqual({ z: "12.0", lat, lon });
    await openFilters(page);
    await expect(filterBox(page)).toHaveValue("type=W");
  });
});

// The map reports its view right after the start; that write used to drop the filter's from the link.
test.describe("link", () => {
  test("has the view and the filter from the start", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await expect.poll(() => linkFilter(page)).toBe("type=W|B|E|G|C|D|L|N&ssid=_%&time[gt]=1d");
    await expect.poll(() => view(page).z).toBe("14.0");

    // The installed app's start: the saved view and filter go into the link as well.
    await enterFilter(page, "type=W");
    await page.goto("/");
    await expect.poll(() => linkFilter(page)).toBe("type=W");
    await expect.poll(() => view(page).z).toBe("14.0");
  });

  test("keeps a filter change when the view changes right after it", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await enterFilter(page, "type=W");
    await zoomOut(page);
    await expect.poll(() => view(page).z).toBe("13.0");
    await expect.poll(() => linkFilter(page)).toBe("type=W");
  });
});
