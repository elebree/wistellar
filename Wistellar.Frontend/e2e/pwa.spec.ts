import type { BrowserContext, Page } from "@playwright/test";
import { test, expect, isTile, stubToken } from "./stub-api";
import { canvas, menuButton, visitMap, waitForNetworkAtCenter } from "./map";
import { style } from "./stub-data";

// The app as an installed PWA: the web app manifest and the service worker. The only spec that
// lets the service worker run (see playwright.config.ts).

test.use({ serviceWorkers: "allow" });

test.beforeEach(async ({ context }) => {
  // The service worker caches the base map style itself, and only context routes see the
  // requests it makes. Without this it would cache the real style, which loads real tiles.
  await context.route("**/liberty.json", (route) => route.fulfill({ json: style }));
});

/** Waits until the service worker has cached the app and controls the page. */
async function waitForServiceWorker(page: Page) {
  await page.waitForFunction(() => navigator.serviceWorker.controller !== null);
}

const openFreeMap = "https://tiles.openfreemap.org";

/**
 * Stands in for OpenFreeMap: a style with one vector source, its TileJSON and empty tiles. Like
 * the real one, the TileJSON points at tile URLs that carry the planet version.
 */
class StubBasemap {
  /** The planet version the TileJSON points at. */
  version = "20990101_000000_pt";
  /**
   * Which requests fail: tiles only is a connection that drops right after the TileJSON, and
   * `server` is OpenFreeMap answering everything with a 503.
   */
  failing: "nothing" | "tiles" | "everything" | "server" = "nothing";
  /** Paths of the tile requests that reached OpenFreeMap rather than a cache. */
  readonly tileRequests: string[] = [];

  async install(context: BrowserContext) {
    const style = {
      version: 8,
      sources: { openmaptiles: { type: "vector", url: `${openFreeMap}/planet` } },
      layers: [{ id: "water", type: "fill", source: "openmaptiles", "source-layer": "water" }],
    };
    // Newer than the beforeEach route, so it wins.
    await context.route("**/liberty.json", (route) => route.fulfill({ json: style }));

    const headers = { "access-control-allow-origin": "*" };
    await context.route(`${openFreeMap}/**`, (route) => {
      const { pathname } = new URL(route.request().url());
      if (this.failing === "everything") return route.abort("internetdisconnected");
      if (this.failing === "server") return route.fulfill({ headers, status: 503, body: "Service Unavailable" });
      if (pathname === "/planet") {
        const tiles = [`${openFreeMap}/planet/${this.version}/{z}/{x}/{y}.pbf`];
        return route.fulfill({ headers, json: { tilejson: "3.0.0", tiles, minzoom: 0, maxzoom: 14 } });
      }
      this.tileRequests.push(pathname);
      if (this.failing === "tiles") return route.abort("internetdisconnected");
      return route.fulfill({ headers, contentType: "application/vnd.mapbox-vector-tile", body: "" });
    });
  }
}

/**
 * Reloads the page and collects the base map tiles the new page gets, and whether the service
 * worker answered. Responses for the page before the reload can still come in after it starts.
 */
async function reloadWatchingTiles(page: Page) {
  const tiles: { path: string; status: number; fromServiceWorker: boolean }[] = [];
  let reloaded = false;
  page.on("framenavigated", (frame) => {
    if (frame === page.mainFrame()) reloaded = true;
  });
  page.on("response", (response) => {
    const url = new URL(response.url());
    if (reloaded && url.origin === openFreeMap && url.pathname.endsWith(".pbf")) {
      tiles.push({ path: url.pathname, status: response.status(), fromServiceWorker: response.fromServiceWorker() });
    }
  });
  await page.reload();
  return tiles;
}

/** URL -> URL of the response stored for it, for everything in the base map cache. */
function basemapCache(page: Page) {
  return page.evaluate(async () => {
    const cache = await caches.open("wistellar-basemap");
    const entries = await Promise.all((await cache.keys()).map(async (key) => [key.url, (await cache.match(key))!.url]));
    return Object.fromEntries(entries) as Record<string, string>;
  });
}

const cachedTiles = async (page: Page) =>
  Object.entries(await basemapCache(page)).filter(([key]) => key.endsWith(".pbf"));

/**
 * Opens the map with the service worker in control, so every base map request passes it, and
 * waits until it has cached the TileJSON and every tile the map asked for.
 */
async function visitWithServiceWorker(page: Page, basemap: StubBasemap) {
  await visitMap(page);
  await waitForServiceWorker(page);
  await page.reload();
  await expect.poll(async () => (await basemapCache(page))[`${openFreeMap}/planet`]).toBeTruthy();
  await expect.poll(async () => (await cachedTiles(page)).length).toBeGreaterThan(0);
  await expect.poll(async () => (await cachedTiles(page)).length).toBe(new Set(basemap.tileRequests).size);
}

/** Width and height from a PNG's header. */
function pngSize(png: Buffer) {
  return `${png.readUInt32BE(16)}x${png.readUInt32BE(20)}`;
}

test("is installable", async ({ page }) => {
  await visitMap(page);
  await waitForServiceWorker(page);

  const cdp = await page.context().newCDPSession(page);
  const { errors } = await cdp.send("Page.getAppManifest");
  expect(errors).toEqual([]);
  // Asks Chrome itself. Test contexts are incognito, where Chrome never offers to install, so
  // that one error is about the test browser rather than the app.
  const { installabilityErrors } = await cdp.send("Page.getInstallabilityErrors");
  expect(installabilityErrors.filter((e) => e.errorId !== "in-incognito")).toEqual([]);
});

test("declares icons that match their files", async ({ page }) => {
  await visitMap(page);
  const href = await page.locator('link[rel="manifest"]').getAttribute("href");
  const manifestUrl = new URL(href!, page.url());
  const manifest = await (await page.request.get(manifestUrl.href)).json();

  const icons: { src: string; sizes: string; type: string; purpose?: string }[] = manifest.icons;
  expect(icons.map((i) => i.purpose ?? "any")).toContain("maskable");
  for (const icon of icons) {
    const response = await page.request.get(new URL(icon.src, manifestUrl).href);
    expect(response.ok(), icon.src).toBe(true);
    expect(response.headers()["content-type"], icon.src).toContain(icon.type);
    if (icon.type === "image/png") expect(pngSize(await response.body()), icon.src).toBe(icon.sizes);
  }

  const touchIcon = await page.locator('link[rel="apple-touch-icon"]').getAttribute("href");
  const response = await page.request.get(new URL(touchIcon!, page.url()).href);
  expect(pngSize(await response.body())).toBe("180x180");
});

test("starts without a connection once it has been opened", async ({ page, context }) => {
  await visitMap(page);
  await waitForServiceWorker(page);

  await context.setOffline(true);
  const response = await page.reload();

  expect(response?.fromServiceWorker()).toBe(true);
  await expect(canvas(page)).toBeVisible();
  await menuButton(page).click();
  await expect(page.getByRole("menuitem")).toHaveText(["Sign in"]);
});

test("falls back to the cached app when the server does not answer", async ({ page, context }) => {
  await visitMap(page);
  await waitForServiceWorker(page);

  // A connection that is up but goes nowhere: the page request never gets an answer.
  let held = false;
  await context.route(
    (url) => url.pathname === "/",
    () => {
      held = true;
    },
  );
  const response = await page.reload();

  expect(held).toBe(true);
  expect(response?.fromServiceWorker()).toBe(true);
  await expect(canvas(page)).toBeVisible();
});

test("falls back to the cached app when the server answers with an error", async ({ page, context }) => {
  await visitMap(page);
  await waitForServiceWorker(page);

  // A reverse proxy in front of a server that is down.
  let answered = false;
  await context.route(
    (url) => url.pathname === "/",
    (route) => {
      answered = true;
      return route.fulfill({ status: 503, contentType: "text/html", body: "<h1>503 Service Unavailable</h1>" });
    },
  );
  const response = await page.reload();

  expect(answered).toBe(true);
  expect(response?.fromServiceWorker()).toBe(true);
  expect(response?.status()).toBe(200);
  await expect(canvas(page)).toBeVisible();
});

test("leaves network data to the server", async ({ page, api }) => {
  await visitMap(page, { token: stubToken() });
  await waitForServiceWorker(page);

  // Reloaded, so that every request the map makes passes the service worker.
  const tileResponses: boolean[] = [];
  page.on("response", (r) => {
    if (isTile(new URL(r.url()))) tileResponses.push(r.fromServiceWorker());
  });
  const tilesBefore = api.tiles.length;
  await page.reload();

  await waitForNetworkAtCenter(page);
  expect(api.tiles.length).toBeGreaterThan(tilesBefore);
  expect(tileResponses.length).toBeGreaterThan(0);
  expect(tileResponses).not.toContain(true);
});

test.describe("base map", () => {
  let basemap: StubBasemap;

  test.beforeEach(async ({ context }) => {
    basemap = new StubBasemap();
    await basemap.install(context);
  });

  test("keeps tiles by position and doesn't fetch them again", async ({ page }) => {
    await visitWithServiceWorker(page, basemap);

    for (const [key, url] of await cachedTiles(page)) {
      expect(key).toMatch(/\/planet\/\d+\/\d+\/\d+\.pbf$/);
      expect(url).toContain(`/planet/${basemap.version}/`);
    }

    const requestsBefore = basemap.tileRequests.length;
    const tiles = await reloadWatchingTiles(page);
    await expect.poll(() => tiles.length).toBeGreaterThan(0);
    // Long enough for a request to go out if one were going to.
    await page.waitForTimeout(1000);
    expect(tiles.filter((t) => !t.fromServiceWorker)).toEqual([]);
    expect(basemap.tileRequests).toHaveLength(requestsBefore);
  });

  test("shows the base map it has shown before without a connection", async ({ page, context }) => {
    await visitWithServiceWorker(page, basemap);

    basemap.failing = "everything";
    await context.setOffline(true);
    const tiles = await reloadWatchingTiles(page);

    await expect(canvas(page)).toBeVisible();
    await expect.poll(() => tiles.length).toBeGreaterThan(0);
    expect(tiles.filter((t) => t.status !== 200 || !t.fromServiceWorker)).toEqual([]);
  });

  test("shows the base map it has shown before while OpenFreeMap answers with errors", async ({ page }) => {
    await visitWithServiceWorker(page, basemap);

    basemap.failing = "server";
    const tiles = await reloadWatchingTiles(page);

    await expect.poll(() => tiles.length).toBeGreaterThan(0);
    expect(tiles.filter((t) => t.status !== 200 || !t.fromServiceWorker)).toEqual([]);
  });

  test("shows the old tiles after a planet update until the new ones arrive", async ({ page }) => {
    await visitWithServiceWorker(page, basemap);

    // The new TileJSON comes through, its tiles don't.
    basemap.version = "20990108_000000_pt";
    basemap.failing = "tiles";
    const tiles = await reloadWatchingTiles(page);

    await expect.poll(() => tiles.length).toBeGreaterThan(0);
    expect(tiles.filter((t) => !t.path.includes(basemap.version))).toEqual([]);
    expect(tiles.filter((t) => t.status !== 200 || !t.fromServiceWorker)).toEqual([]);
    // Each old tile that stood in sent for its new version.
    await expect.poll(() => basemap.tileRequests.filter((p) => p.includes(basemap.version)).length).toBeGreaterThan(0);

    // Back online, the new tiles replace the old ones in the cache.
    basemap.failing = "nothing";
    await page.reload();
    await expect
      .poll(async () => (await cachedTiles(page)).filter(([, url]) => !url.includes(basemap.version)))
      .toEqual([]);
  });
});
