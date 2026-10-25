import { expect, type Page } from "@playwright/test";
import { positionOf, user } from "./stub-api";

export const menuButton = (page: Page) => page.locator(".app-menu > button");
export const dialog = (page: Page) => page.locator("dialog");
export const popup = (page: Page) => page.locator(".maplibregl-popup");
export const canvas = (page: Page) => page.locator(".maplibregl-canvas");

/** Opens the map centered on a stub network, optionally with a stored session. */
export async function visitMap(
  page: Page,
  { at = "00:00:00:00:00:01", zoom = 14, token }: { at?: string; zoom?: number; token?: string } = {},
) {
  if (token) await page.addInitScript((t) => localStorage.setItem("access_token", t), token);
  const [lon, lat] = positionOf(at);
  await page.goto(`/?z=${zoom}&lat=${lat}&lon=${lon}`);
  await expect(canvas(page)).toBeVisible();
}

export async function openSignIn(page: Page) {
  await menuButton(page).click();
  await page.getByRole("menuitem", { name: "Sign in" }).click();
  await expect(dialog(page)).toHaveAttribute("open");
}

export async function fillInSignIn(page: Page, password = user.password) {
  await page.locator("#username").fill(user.name);
  await page.locator("#password").fill(password);
}

export async function signIn(page: Page) {
  await openSignIn(page);
  await fillInSignIn(page);
  await page.locator('dialog button[type="submit"]').click();
  await expect(dialog(page)).not.toHaveAttribute("open");
}

async function canvasCenter(page: Page) {
  const box = (await canvas(page).boundingBox())!;
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/**
 * Waits until the network under the map's center is drawn. The map shows a pointer cursor over a
 * network, so this keeps moving the mouse there until it does.
 */
export async function waitForNetworkAtCenter(page: Page) {
  const { x, y } = await canvasCenter(page);
  await expect(async () => {
    await page.mouse.move(x + 2, y + 2);
    await page.mouse.move(x, y);
    expect(await canvas(page).evaluate((el) => el.style.cursor)).toBe("pointer");
  }).toPass();
}

/** Clicks the network at the map's center and waits for its popup. */
export async function openPopupAtCenter(page: Page) {
  await waitForNetworkAtCenter(page);
  const { x, y } = await canvasCenter(page);
  await page.mouse.click(x, y);
  await expect(popup(page)).toBeVisible();
}

/** The value cell of a popup detail row. */
export function detail(page: Page, label: string) {
  return popup(page)
    .locator("dt")
    .filter({ hasText: new RegExp(`^${label}$`) })
    .locator("xpath=following-sibling::dd[1]");
}

/** Zooms out with the map's own control, which makes the map ask for new tiles. */
export async function zoomOut(page: Page) {
  await page.locator(".maplibregl-ctrl-zoom-out").click();
}
