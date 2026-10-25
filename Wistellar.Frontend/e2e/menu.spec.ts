import { test, expect, stubToken } from "./stub-api";
import { canvas, menuButton, visitMap } from "./map";

test.describe("menu", () => {
  test("opens with the menu ARIA state and focuses the first item", async ({ page }) => {
    await visitMap(page);
    await expect(menuButton(page)).toHaveAttribute("aria-haspopup", "menu");
    await expect(menuButton(page)).toHaveAttribute("aria-expanded", "false");
    await menuButton(page).click();
    await expect(menuButton(page)).toHaveAttribute("aria-expanded", "true");
    await expect(page.getByRole("menuitem", { name: "Sign in" })).toBeFocused();
  });

  test("closes on Escape and gives focus back to the button", async ({ page }) => {
    await visitMap(page);
    await menuButton(page).click();
    await page.keyboard.press("Escape");
    await expect(page.getByRole("menu")).toHaveCount(0);
    await expect(menuButton(page)).toBeFocused();
  });

  test("opens from the keyboard with ArrowDown", async ({ page }) => {
    await visitMap(page);
    await menuButton(page).focus();
    await page.keyboard.press("ArrowDown");
    await expect(page.getByRole("menu")).toBeVisible();
  });

  test("closes on a click outside", async ({ page }) => {
    await visitMap(page);
    await menuButton(page).click();
    await canvas(page).click({ position: { x: 400, y: 400 } });
    await expect(page.getByRole("menu")).toHaveCount(0);
  });

  test("shows the user and Sign out when signed in, and is named after the user", async ({ page }) => {
    await visitMap(page, { token: stubToken() });
    await menuButton(page).click();
    await expect(page.getByRole("menuitem")).toHaveText(["Sign out"]);
    await expect(page.getByRole("menu", { name: "Signed in as janedoe" })).toBeVisible();
  });
});
