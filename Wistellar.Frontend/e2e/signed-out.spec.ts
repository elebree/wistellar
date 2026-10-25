import { test, expect } from "./stub-api";
import { menuButton, visitMap } from "./map";

test.describe("signed out", () => {
  test.beforeEach(async ({ page }) => {
    await visitMap(page);
  });

  test("shows the base map without asking for network data", async ({ page, api }) => {
    await expect.poll(() => api.style.length).toBeGreaterThan(0);
    await expect(page.locator("#filter")).toHaveCount(0);
    // Long enough for the map to have asked for tiles if it were going to.
    await page.waitForTimeout(1000);
    expect(api.geo).toHaveLength(0);
  });

  test("offers only Sign in in the menu", async ({ page }) => {
    await menuButton(page).click();
    await expect(page.getByRole("menuitem")).toHaveText(["Sign in"]);
  });
});
