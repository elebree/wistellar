import { test, expect, isTile, stubToken, user } from "./stub-api";
import { dialog, visitMap } from "./map";

test.describe("stored session", () => {
  const storedToken = (page: import("@playwright/test").Page) =>
    page.evaluate(() => localStorage.getItem("access_token"));

  test("restores a valid token on load", async ({ page }) => {
    const tileRequest = page.waitForRequest((request) => isTile(new URL(request.url())));
    await visitMap(page, { token: stubToken() });
    await expect(page.locator("#filter")).toBeVisible();
    await tileRequest;
  });

  test("drops a malformed token and stays signed out", async ({ page }) => {
    await visitMap(page, { token: "not-a-jwt" });
    await expect.poll(() => storedToken(page)).toBeNull();
    await expect(page.locator("#filter")).toHaveCount(0);
    await expect(dialog(page)).not.toHaveAttribute("open");
  });

  test("drops an expired token and asks to sign in again", async ({ page }) => {
    await visitMap(page, { token: stubToken(user.name, -60) });
    await expect.poll(() => storedToken(page)).toBeNull();
    await expect(dialog(page)).toHaveAttribute("open");
    await expect(page.locator('dialog [role="status"]')).toContainText("Your session has expired");
  });
});
