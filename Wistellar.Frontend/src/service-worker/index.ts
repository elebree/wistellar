import { dev, version } from "$app/env";
import { assets, immutable, prerendered } from "$app/manifest";
import { self } from "$app/service-worker";

// Lets the installed app start, and show the base map it has shown before, without a connection,
// for example out on a survey. It keeps two caches:
//
// - The app itself: the build output, the static files and the prerendered page. One per build.
// - The OpenFreeMap base map: the tiles, fonts and sprites the map has loaded. Kept across builds,
//   within a storage budget.
//
// Every other request goes to the network as if there were no service worker. API calls and
// network tiles (/api, /geo) carry the user's token and change with every import.
//
// SvelteKit registers this file itself. A new build always changes it, because `version` and the
// file list change, so the browser installs the new worker and its cache on its next update check.

const appCache = `wistellar-app-${version}`;
const basemapCache = "wistellar-basemap";

/** Where static/liberty.json loads the base map from. */
const basemapOrigin = "https://tiles.openfreemap.org";

/**
 * How much the app may keep in storage, base map included. Past it, the oldest base map entries
 * go. A city tile is up to 400 kB, most are far smaller, so this holds a few thousand tiles.
 */
const storageBudget = 300 * 1024 * 1024;

/**
 * Both caches hold exactly what was fetched for them, so a `Vary` header (`Vary: Origin` from CORS
 * middleware, say, or whatever a reverse proxy adds) must not stop a match. Module scripts are
 * CORS requests, which the precache requests are not.
 */
const matchOptions: CacheQueryOptions = { ignoreVary: true };

/**
 * How long a launch waits for the server before falling back to the cache. Wardriving connections
 * are often up but useless, which would otherwise mean a long blank screen.
 */
const networkTimeoutMs = 3000;

/**
 * Absolute URLs of the files the app needs to start. SvelteKit gives paths relative to the base
 * path, which is the worker's scope, and the prerendered page's path is empty.
 */
const appFiles = new Set(
    [...immutable, ...assets, ...prerendered].map(({ path }) => new URL(path, self.registration.scope).href),
);

/**
 * OpenFreeMap's vector tile URLs carry the planet version, and its TileJSON always points at the
 * newest one. Cached under their URLs, every planet update would orphan the whole offline map, so
 * they are cached under their z/x/y instead.
 */
const versionedTile = /^(https:\/\/[^/]+\/planet)\/[^/]+(\/\d+\/\d+\/\d+\.pbf)$/;

self.addEventListener("install", (event) => {
    event.waitUntil(precache().then(() => self.skipWaiting()));
});

self.addEventListener("activate", (event) => {
    event.waitUntil(dropOldCaches().then(() => self.clients.claim()));
});

self.addEventListener("fetch", (event) => {
    const { request } = event;
    if (request.method !== "GET") return;

    if (new URL(request.url).origin === basemapOrigin) {
        event.respondWith(basemap(event));
        return;
    }
    // Under `vite dev` the app is compiled on demand and changes under the worker, so there only
    // the base map is cached. The app itself starts offline from a production build.
    if (dev) return;
    if (request.mode === "navigate") event.respondWith(navigate(request));
    else if (appFiles.has(request.url)) event.respondWith(fromAppCache(request));
});

async function precache() {
    if (dev) return;
    const cache = await caches.open(appCache);
    // Revalidates instead of trusting the HTTP cache, which may still hold the previous build's
    // page. That page would point at files this cache doesn't have.
    await cache.addAll(Array.from(appFiles, (url) => new Request(url, { cache: "no-cache" })));
}

/** Deletes earlier builds' caches, and nothing that isn't ours. The base map cache stays. */
async function dropOldCaches() {
    for (const key of await caches.keys()) {
        if (key.startsWith("wistellar-") && key !== appCache && key !== basemapCache) await caches.delete(key);
    }
}

/**
 * Pages come from the network, so a server update shows up on the next launch. If the network
 * fails or stalls, or the server answers with an error, such as a reverse proxy's 503 while the
 * server is down, the app's page comes from the cache instead. The query string holds only the map
 * view and filter, so any query matches.
 */
async function navigate(request: Request): Promise<Response> {
    // The cached page belongs to this worker's build, so it always matches the cached files.
    // A newer page from the network may not, so it never replaces it.
    const cached = await caches.match(request, { ...matchOptions, cacheName: appCache, ignoreSearch: true });
    const network = fetch(request);
    return cached ? networkOr(network, cached) : network;
}

/** Build output is content-hashed and the rest is part of this build, so the cache wins. */
async function fromAppCache(request: Request): Promise<Response> {
    return (await caches.match(request, { ...matchOptions, cacheName: appCache })) ?? fetch(request);
}

async function basemap(event: FetchEvent): Promise<Response> {
    const { request } = event;
    const cache = await caches.open(basemapCache);
    const key = request.url.replace(versionedTile, "$1$2");
    const cached = await cache.match(key, matchOptions);

    // A document without a file extension, like the TileJSON, says which tiles are current, so it
    // comes from the network when it can.
    if (!/\.\w+$/.test(new URL(request.url).pathname)) {
        const network = fetch(request);
        event.waitUntil(network.then((response) => keep(cache, key, response), () => {}));
        return cached ? networkOr(network, cached) : network;
    }

    // Tiles, fonts and sprites never change under their URL, so the cache wins. A tile of an
    // earlier planet version is shown at once while the current one is fetched for next time.
    if (cached) {
        if (cached.url !== request.url) {
            event.waitUntil(fetch(request.url).then((response) => keep(cache, key, response), () => {}));
        }
        return cached;
    }
    const response = await fetch(request);
    event.waitUntil(keep(cache, key, response));
    return response;
}

/**
 * Caches a copy of a successful base map response, then makes room if the app is over its storage
 * budget. Call it before the response is handed on, which reads its body.
 */
async function keep(cache: Cache, key: string, response: Response) {
    if (!response.ok) return;
    try {
        await cache.put(key, response.clone());
        await trimBasemap();
    } catch {
        // Out of space, or the map cancelled the request. It got its response either way.
    }
}

let lastTrim = 0;

/**
 * Over budget, deletes the oldest quarter of the base map cache, which leaves room for a while
 * instead of trimming on every tile. Checks at most once a minute.
 */
async function trimBasemap() {
    if (Date.now() - lastTrim < 60_000) return;
    lastTrim = Date.now();

    const { usage = 0 } = await navigator.storage.estimate();
    if (usage <= storageBudget) return;

    const cache = await caches.open(basemapCache);
    const keys = await cache.keys(); // Oldest first.
    await Promise.all(keys.slice(0, Math.ceil(keys.length / 4)).map((key) => cache.delete(key)));
}

/**
 * The network's response, or `fallback` if the network fails, takes too long or answers with a
 * server error (5xx). Client errors still come through: a 401 from an authenticating proxy, say,
 * is something to act on, not to hide.
 */
async function networkOr(network: Promise<Response>, fallback: Response): Promise<Response> {
    const timeout = new Promise<undefined>((resolve) => setTimeout(resolve, networkTimeoutMs));
    try {
        const response = await Promise.race([network, timeout]);
        return response && response.status < 500 ? response : fallback;
    } catch {
        return fallback;
    }
}
