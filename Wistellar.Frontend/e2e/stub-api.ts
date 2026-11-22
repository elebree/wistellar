import { test as base, expect, type Page, type Request } from "@playwright/test";
import { geoJSONToTile } from "@maplibre/geojson-vt";
import { fromGeojsonVt } from "@maplibre/vt-pbf";
import { networks, style } from "./stub-data";

// Stands in for the Wistellar server: every /api and /geo request is answered here from the stub
// networks in stub-data.ts. Tests import `test` and `expect` from this module to get it.

export const user = { name: "janedoe", password: "Stub-Passw0rd!" };

export const isTile = (url: URL) => /^\/geo\/tiles\/\d+\/\d+\/\d+\.pbf$/.test(url.pathname);
export const isSignIn = (url: URL) => url.pathname === "/api/v2/activate";

/** [lon, lat] of a stub network, for centering the map on it. */
export function positionOf(bssid: string): [number, number] {
  const feature = networks.features.find((f) => f.id === bssid);
  if (!feature) throw new Error(`No stub network ${bssid}`);
  return feature.geometry.coordinates as [number, number];
}

/** A JWT the SPA can read. It never checks the signature, so there is none. */
export function stubToken(name = user.name, expiresInSeconds = 3600): string {
  const part = (value: object) => Buffer.from(JSON.stringify(value)).toString("base64url");
  return [
    part({ alg: "HS256", typ: "JWT" }),
    part({
      "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": name,
      exp: Math.floor(Date.now() / 1000) + expiresInSeconds,
      iss: "wistellar",
      aud: "wistellar",
    }),
    "stub-signature",
  ].join(".");
}

// Same split as GeoJsonController: wifi, bluetooth (B, E, F) and cell (G, C, L, D, N).
function layerOf(type: string) {
  if (type === "W") return "wifi";
  return "BEF".includes(type) ? "bluetooth" : "cell";
}

/** A dwell bound in seconds: plain seconds, or one amount with a unit (m h d; M and y approximated). */
function seconds(text: string): number {
  const [, amount, unit] = text.match(/^(\d+)([mhdMy]?)$/) ?? [];
  const size = { "": 1, m: 60, h: 3600, d: 86400, M: 30 * 86400, y: 365 * 86400 }[unit ?? ""] ?? NaN;
  return Number(amount) * size;
}

/** Whether a stub network passes the filter's bounds on range, dwell and observations, as on the server. */
function withinBounds(properties: Record<string, number>, filter: URLSearchParams): boolean {
  const bounds = [
    ["range", properties.dist, Number],
    ["dwell", properties.dur, seconds],
    ["locations", properties.loc, Number],
  ] as const;
  return bounds.every(([name, value, read]) => {
    const gt = filter.get(`${name}[gt]`);
    const lt = filter.get(`${name}[lt]`);
    return (gt === null || value > read(gt)) && (lt === null || value < read(lt));
  });
}

/** SQLite's LIKE: `%` any text, `_` one character, case-insensitive. */
const likePattern = (pattern: string) =>
  new RegExp(`^${pattern.replace(/[.*+?^${}()|[\]\\]/g, "\\$&").replace(/%/g, ".*").replace(/_/g, ".")}$`, "is");

/**
 * A vector tile as /geo/tiles builds it: one layer per kind, features carry only BSSID and type.
 */
function vectorTile(z: number, x: number, y: number, filter: URLSearchParams): Buffer {
  const types = filter.get("type");
  const shown = types ? types.split("|") : undefined;
  const patterns = ["ssid", "bssid"].flatMap((name) => {
    const value = filter.get(name);
    return value ? [{ name, alternatives: value.split("|").map(likePattern) }] : [];
  });
  const layers: Parameters<typeof fromGeojsonVt>[0] = {};
  for (const name of ["wifi", "bluetooth", "cell"]) {
    // No feature id: the server's ids are BSSIDs, which a vector tile can't hold, so it sends them
    // as a property instead.
    const features = networks.features
      .filter((f) => layerOf(f.properties?.type) === name)
      .filter((f) => !shown || shown.includes(f.properties?.type))
      .filter((f) =>
        patterns.every(({ name, alternatives }) =>
          alternatives.some((pattern) => pattern.test(String(f.properties?.[name] ?? ""))),
        ),
      )
      .filter((f) => withinBounds(f.properties as Record<string, number>, filter))
      .map((f) => ({
        type: "Feature" as const,
        geometry: f.geometry,
        properties: { _NetTopologySuite_id: f.id, type: f.properties?.type },
      }));
    const tile = geoJSONToTile({ type: "FeatureCollection", features }, z, x, y, {
      extent: 4096,
      clip: true,
    });
    if (tile) layers[name] = tile;
  }
  return Buffer.from(fromGeojsonVt(layers, { version: 2, extent: 4096 }));
}

/** The requests the stub server saw, for checks such as "no network data was requested". */
export class StubApi {
  readonly style: Request[] = [];
  readonly signIns: Request[] = [];
  readonly geo: Request[] = [];
  readonly tiles: Request[] = [];
  readonly networkInfo: Request[] = [];

  constructor(private readonly page: Page) {}

  async install() {
    const page = this.page;
    const query = (request: Request, name: string) => new URL(request.url()).searchParams.get(name);

    // Routes run newest first, so this catch-all only answers what nothing below does, instead
    // of letting it reach the dev server's proxy. A test can override any route the same way.
    await page.route(
      (url) => /^\/(api|geo)\//.test(url.pathname),
      (route) => route.fulfill({ status: 501, body: "Not stubbed" }),
    );
    await page.route("**/liberty.json", (route) => route.fulfill({ json: style }));

    await page.route(isSignIn, (route) => {
      const form = new URLSearchParams(route.request().postData() ?? "");
      const valid = form.get("credential_0") === user.name && form.get("credential_1") === user.password;
      return route.fulfill(valid ? { json: { token: stubToken() } } : { status: 401 });
    });

    await page.route(isTile, (route) => {
      const { pathname, searchParams } = new URL(route.request().url());
      const [z, x, y] = pathname.match(/(\d+)\/(\d+)\/(\d+)\.pbf$/)!.slice(1).map(Number);
      const body = vectorTile(z, x, y, searchParams);
      return route.fulfill({ contentType: "application/vnd.mapbox-vector-tile", body });
    });

    await page.route(
      (url) => url.pathname === "/geo/network",
      (route) => {
        const bssid = query(route.request(), "bssid");
        const features = networks.features.filter((f) => f.id === bssid);
        return route.fulfill({ json: { type: "FeatureCollection", features } });
      },
    );

    await page.route(
      (url) => url.pathname === "/geo/location",
      (route) => {
        // Two observations a few metres apart around the network.
        const [lon, lat] = positionOf(query(route.request(), "bssid") ?? "");
        const features = [
          [lon, lat],
          [lon + 0.0001, lat + 0.00005],
        ].map((coordinates, i) => ({
          type: "Feature",
          geometry: { type: "Point", coordinates },
          properties: { level: -50 - i * 10 },
        }));
        return route.fulfill({ json: { type: "FeatureCollection", features } });
      },
    );

    page.on("request", (request) => {
      const url = new URL(request.url());
      if (url.pathname === "/liberty.json") this.style.push(request);
      if (isSignIn(url)) this.signIns.push(request);
      if (url.pathname.startsWith("/geo/")) this.geo.push(request);
      if (isTile(url)) this.tiles.push(request);
      if (url.pathname === "/geo/network") this.networkInfo.push(request);
    });
  }
}

export const test = base.extend<{ api: StubApi }>({
  api: [
    async ({ page }, use) => {
      const api = new StubApi(page);
      await api.install();
      await use(api);
    },
    { auto: true },
  ],
});

export { expect };
