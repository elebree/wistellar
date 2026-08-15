# Wistellar.Frontend

The Wistellar map interface: a [SvelteKit](https://svelte.dev/docs/kit) single-page app that renders
wireless network observations on a [MapLibre GL](https://maplibre.org/) map, backed by vector tiles
generated on the fly by the Wistellar API.

It is built with `adapter-static` and served as plain files by `Wistellar.Server`, so there is no Node
runtime in production — Node is a build-time requirement only.

## Prerequisites

- **Node.js 20 or newer** (Vite 6 and lightningcss both refuse to run on older releases)
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
pnpm run check     # svelte-check — the only static analysis this project has
pnpm run build     # production build
```

Building `Wistellar.Server` also builds this project through the `.esproj` wrapper, which exists purely
so Visual Studio and MSBuild can drive pnpm. An outdated Node on `PATH` therefore fails the *C#* build;
build `Wistellar.Core` directly to iterate on C# alone.

## Layout

| Path | Contents |
| --- | --- |
| `src/routes/` | SvelteKit routes: the map page and the login page |
| `src/components/` | `MapLibre.svelte` (map, layers, popups) and `Login.svelte` |
| `src/lib/authService.ts` | Token storage and expiry checks against the JWT |
| `static/liberty.json` | The base map style |

The JWT is attached to tile requests through MapLibre's `transformRequest` hook in
`MapLibre.svelte`; tiles carry only an id and a type, and full attributes are fetched from
`/geo/network?bssid=` when a feature is clicked.
