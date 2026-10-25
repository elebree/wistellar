import { test, expect, stubToken } from "./stub-api";
import { menuButton, visitMap } from "./map";

test.describe("phone layout", () => {
  test.use({ viewport: { width: 390, height: 844 } });

  test("keeps the filter box and the menu button side by side in the top row", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await expect(page.locator("#filter")).toBeVisible();
    const filter = (await page.locator("#filter").boundingBox())!;
    const button = (await menuButton(page).boundingBox())!;
    expect(filter.x + filter.width, "filter ends before the button").toBeLessThanOrEqual(button.x);
    expect(Math.abs(filter.y - button.y), "same top").toBeLessThan(1);
    expect(Math.abs(filter.height - button.height), "same height").toBeLessThan(1);
    expect(Math.abs(button.x + button.width - (390 - 10)), "10px from the right edge").toBeLessThan(1);
  });
});
