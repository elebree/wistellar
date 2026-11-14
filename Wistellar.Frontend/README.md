# Wistellar.Frontend

The Wistellar map interface: a [SvelteKit](https://svelte.dev/docs/kit) single-page app that renders
wireless network observations on a [MapLibre GL](https://maplibre.org/) map, backed by vector tiles
generated on the fly by the Wistellar API.

It is built with `adapter-static` and served as plain files by `Wistellar.Server`, so there is no Node
runtime in production — Node is a build-time requirement only.

## Prerequisites

- **Node.js 22 or newer**
- **pnpm** — the lockfile in this directory is a pnpm lockfile

## Developing

The app needs the API for data and for authentication, so run the ASP.NET host first:

```bash
dotnet run --project ../Wistellar.Server
```

Then, in this directory:

```bash
pnpm install
pnpm run dev       # https://localhost:5173
```

The dev server proxies `/api/` and `/geo/` through to the ASP.NET host, picking the target from
`ASPNETCORE_URLS` and falling back to `https://localhost:7188`. On first run `vite.config.ts` exports a
development certificate via `dotnet dev-certs https`, so the .NET SDK must be on `PATH` even when you
are only touching front-end code.

## Checking and building

```bash
pnpm run check     # svelte-check, plus tsc over e2e/ and the service worker — the only static analysis
pnpm run build     # production build
```

Building `Wistellar.Server` also builds this project through the `.esproj` wrapper, which exists purely
so Visual Studio and MSBuild can drive pnpm. An outdated Node on `PATH` therefore fails the *C#* build;
build `Wistellar.Core` directly to iterate on C# alone.

## Layout

| Path | Contents |
| --- | --- |
| `src/routes/` | SvelteKit routes: the map page and the login page |
| `src/components/` | `MapLibre.svelte` (map, layers, selection), `NetworkPanel.svelte` (details of a clicked network) and `Login.svelte` |
| `src/lib/authService.ts` | Token storage and expiry checks against the JWT |
| `src/service-worker/` | The service worker that lets the installed app start offline; it caches the app and the base map, never API data |
| `src/lib/saved-view.ts` | Keeps the map view and filter across launches, for the installed app, which starts without them in its URL |
| `static/liberty.json` | The base map style |
| `static/manifest.webmanifest` | The web app manifest |
| `static/splash.*`, `src/lib/splash.ts` | The installed app's startup screen (markup in `src/app.html`) |

The JWT is attached to tile requests through MapLibre's `transformRequest` hook in
`MapLibre.svelte`; tiles carry only an id and a type, and full attributes are fetched from
`/geo/network?bssid=` when a feature is clicked.
