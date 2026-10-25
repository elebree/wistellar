import type { Page } from "@playwright/test";
import { test, expect, isSignIn, isTile, stubToken } from "./stub-api";
import { dialog, fillInSignIn, menuButton, openSignIn, visitMap } from "./map";

const submit = (page: Page) => page.locator('dialog button[type="submit"]');
const alert = (page: Page) => page.locator('dialog [role="alert"]');

test.describe("sign-in dialog", () => {
  test.beforeEach(async ({ page }) => {
    await visitMap(page);
  });

  test("opens as a modal from the menu and closes on Escape, giving focus back", async ({ page }) => {
    await openSignIn(page);
    expect(await dialog(page).evaluate((d) => d.matches(":modal"))).toBe(true);
    await expect(page.locator("#username")).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(dialog(page)).not.toHaveAttribute("open");
    await expect(menuButton(page)).toBeFocused();
  });

  test("closes with Cancel and with a click on the backdrop", async ({ page }) => {
    await openSignIn(page);
    await page.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog(page)).not.toHaveAttribute("open");

    await openSignIn(page);
    await page.mouse.click(10, 10);
    await expect(dialog(page)).not.toHaveAttribute("open");
  });

  test("only has its form in the page while open, so browsers fill saved passwords", async ({ page }) => {
    await expect(page.locator("dialog form")).toHaveCount(0);
    await openSignIn(page);
    await expect(page.locator("dialog form")).toHaveCount(1);
  });

  test("checks for empty fields without sending a request", async ({ page, api }) => {
    await openSignIn(page);
    await submit(page).click();
    await expect(dialog(page)).toHaveAttribute("open");
    expect(api.signIns).toHaveLength(0);
  });

  test("explains a wrong password", async ({ page }) => {
    await openSignIn(page);
    await fillInSignIn(page, "wrong-password");
    await submit(page).click();
    await expect(alert(page)).toHaveText("Invalid username or password.");
  });

  test("explains a busy server and an unreachable one", async ({ page }) => {
    await page.route(isSignIn, (route) => route.fulfill({ status: 503 }));
    await openSignIn(page);
    await fillInSignIn(page);
    await submit(page).click();
    await expect(alert(page)).toContainText("The server is busy");

    await page.route(isSignIn, (route) => route.abort("failed"));
    await submit(page).click();
    await expect(alert(page)).toContainText("Server not reachable");
  });

  test("disables the button while signing in", async ({ page }) => {
    await page.route(isSignIn, async (route) => {
      await new Promise((resolve) => setTimeout(resolve, 800));
      await route.fulfill({ json: { token: stubToken() } });
    });
    await openSignIn(page);
    await fillInSignIn(page);
    await submit(page).click();
    await expect(submit(page)).toBeDisabled();
    await expect(submit(page)).toHaveText("Signing in…");
    await expect(dialog(page)).not.toHaveAttribute("open");
  });

  test("signs in where the map is and loads networks with the token", async ({ page }) => {
    const searchBefore = new URL(page.url()).search;
    const signInResponse = page.waitForResponse((response) => isSignIn(new URL(response.url())));
    const tileRequest = page.waitForRequest((request) => isTile(new URL(request.url())));

    await openSignIn(page);
    await fillInSignIn(page);
    await submit(page).click();

    await expect(dialog(page)).not.toHaveAttribute("open");
    await expect(page.locator("#filter")).toBeVisible();
    const { token } = await (await signInResponse).json();
    expect((await (await tileRequest).allHeaders()).authorization).toBe(`Bearer ${token}`);
    expect(new URL(page.url()).search).toBe(searchBefore);
  });

  test("focuses the password when the session expires", async ({ page }) => {
    // Sign-in succeeds, but the first tiles answer 401, which expires the new session at once.
    await page.route(isTile, (route) => route.fulfill({ status: 401 }));
    await openSignIn(page);
    await fillInSignIn(page);
    await submit(page).click();

    await expect(page.locator('dialog [role="status"]')).toContainText("Your session has expired");
    await expect(page.locator("#username")).toHaveValue("janedoe");
    await expect(page.locator("#password")).toBeFocused();
  });
});
