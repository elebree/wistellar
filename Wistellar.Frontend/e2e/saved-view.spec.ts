import type { Page } from "@playwright/test";
import { test, expect, positionOf, stubToken } from "./stub-api";
import { visitMap, zoomOut } from "./map";

// The installed app always starts at the manifest's start_url, which has no view or filter in it.

/** The view as the app writes it into the URL. `z` stays a string: the app formats it as "13.0". */
function view(page: Page) {
  const params = new URL(page.url()).searchParams;
  return { z: params.get("z"), lat: Number(params.get("lat")), lon: Number(params.get("lon")) };
}

const filterBox = (page: Page) => page.locator("#filter");

test.describe("saved view", () => {
  test.beforeEach(async ({ page }) => {
    await visitMap(page, { token: stubToken() });
  });

  test("starts where the map was left, with the same filter", async ({ page }) => {
    await filterBox(page).fill("type=W&ssid=Stub%");
    await filterBox(page).press("Enter");
    await zoomOut(page);
    await expect.poll(() => view(page).z).toBe("13.0");
    const left = view(page);

    await page.goto("/");

    await expect(filterBox(page)).toHaveValue("type=W&ssid=Stub%");
    await expect.poll(() => view(page)).toEqual(left);
  });

  test("leaves a link's view and filter alone", async ({ page }) => {
    await zoomOut(page);
    await expect.poll(() => view(page).z).toBe("13.0");

    const [lon, lat] = positionOf("00:00:00:00:00:02");
    await page.goto(`/?z=12&lat=${lat}&lon=${lon}&type=W`);

    // The app rewrites the view into the URL from the map, so "12.0" means the map is at the link.
    await expect.poll(() => view(page)).toEqual({ z: "12.0", lat, lon });
    await expect(filterBox(page)).toHaveValue("type=W");
  });
});
