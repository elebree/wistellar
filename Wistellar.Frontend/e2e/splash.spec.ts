import type { Page } from "@playwright/test";
import { test, expect } from "./stub-api";

// The installed app's startup screen. Its stylesheet only applies with `display-mode: standalone`,
// which Chrome can't emulate, so `asInstalledApp` serves the page with that stylesheet always on.

const splash = (page: Page) => page.locator("#splash");

/** Serves the page as the installed app sees it. */
async function asInstalledApp(page: Page) {
  await page.route(
    (url) => url.pathname === "/",
    async (route) => {
      const response = await route.fetch();
      const body = (await response.text()).replace('media="(display-mode: standalone)"', 'media="all"');
      await route.fulfill({ response, body });
    },
  );
}

/** Holds back the base map style, and with it the map's first draw, until `release` is called. */
async function holdStyle(page: Page) {
  let release!: () => void;
  const held = new Promise<void>((resolve) => (release = resolve));
  await page.route("**/liberty.json", async (route) => {
    await held;
    await route.fallback();
  });
  return release;
}

test("never shows in a browser tab", async ({ page }) => {
  const release = await holdStyle(page);
  await page.goto("/");
  await expect(splash(page)).toHaveCSS("display", "none");

  release();
  await expect(splash(page)).toHaveCount(0);
});

test.describe("installed app", () => {
  test.beforeEach(async ({ page }) => {
    await asInstalledApp(page);
  });

  test("shows the mark and the name from the first paint, without blocking input", async ({ page }) => {
    await holdStyle(page);
    await page.addInitScript(() =>
      document.addEventListener("DOMContentLoaded", () => {
        (window as any).splashAtStart = getComputedStyle(document.getElementById("splash")!).display;
      }),
    );
    await page.goto("/");
    expect(await page.evaluate(() => (window as any).splashAtStart)).toBe("grid");

    await expect(splash(page)).toBeVisible();
    await expect(splash(page)).toHaveCSS("pointer-events", "none");
    await expect(splash(page)).toContainText("WISTELLAR");
    const mark = splash(page).locator("img");
    await expect.poll(() => mark.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
  });

  test("moves only through animations the browser runs off the main thread", async ({ page }) => {
    await holdStyle(page);
    await page.goto("/");
    await expect(splash(page)).toBeVisible();

    const animations = await splash(page).evaluate((el) =>
      el.getAnimations({ subtree: true }).map((animation) => ({
        name: (animation as CSSAnimation).animationName,
        playState: animation.playState,
        looping: animation.effect?.getTiming().iterations === Infinity,
        properties: [
          ...new Set(
            (animation.effect as KeyframeEffect)
              .getKeyframes()
              .flatMap((frame) =>
                Object.keys(frame).filter((key) => !["offset", "computedOffset", "easing", "composite"].includes(key)),
              ),
          ),
        ],
      })),
    );
    const names = animations.map((a) => a.name);
    for (const name of ["ping", "glow", "name-in"]) {
      expect(names, name).toContain(name);
    }
    // The mark's rings: one animation per still ring a wave lights up.
    expect(names.filter((name) => /^wave-\d+$/.test(name)).length).toBeGreaterThan(10);
    // Leaving after 8 seconds also hides the screen, but nothing moves then. The name comes in once
    // and may be in already.
    const moving = animations.filter((a) => a.name !== "splash-out");
    expect(moving.filter((a) => a.looping && a.playState !== "running")).toEqual([]);
    expect(moving.filter((a) => a.properties.some((p) => p !== "transform" && p !== "opacity"))).toEqual([]);
  });

  test("goes once the map has drawn", async ({ page }) => {
    const release = await holdStyle(page);
    await page.goto("/");
    await expect(splash(page)).toBeVisible();

    release();
    await expect(splash(page)).toHaveCount(0);
  });

  test("goes on its own when the map never loads", async ({ page }) => {
    await page.route("**/liberty.json", (route) => route.abort());
    await page.goto("/");
    await expect(splash(page)).toBeVisible();

    // It gives up 8 seconds after the page opened; run the page's CSS animations faster than that.
    const cdp = await page.context().newCDPSession(page);
    await cdp.send("Animation.enable");
    await cdp.send("Animation.setPlaybackRate", { playbackRate: 20 });
    await expect(splash(page)).toHaveCount(0);
  });
});
