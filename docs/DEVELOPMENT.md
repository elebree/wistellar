# Development

How to build, run and change Wistellar from a source checkout. For running a published image, see the
[README](../README.md).

## Prerequisites

- **.NET 10 SDK**
- **Node.js 22+** and **pnpm**

Building the server also builds the front end, through the `.esproj` wrapper that runs `pnpm install`.
If Node is too old, the *C#* build fails with `MSB3073: "pnpm install" exited with code 1`.

## Running locally

```bash
# API + web UI
dotnet run --project Wistellar.Server
```

The API listens on <https://localhost:7188>. In Development, Swagger at `/swagger` lists every
endpoint.

To work on the front end with hot reload, keep the server running and start Vite alongside it. Vite
proxies `/api` and `/geo` through to the running API:

```bash
cd Wistellar.Frontend
pnpm install
pnpm run dev      # https://localhost:5173
```

User-management commands work from a source checkout too. Pass them after `--`:

```bash
dotnet run --project Wistellar.Server -- --add-user --username alice --password 'S3cret!' --role member
dotnet run --project Wistellar.Server -- --update-user --username alice --role moderator
dotnet run --project Wistellar.Server -- --delete-user --username alice
```

## Checks and tests

```bash
dotnet test Wistellar.Tests/Wistellar.Tests.csproj    # server-side tests (xUnit)

cd Wistellar.Frontend
pnpm run check                                         # svelte-check type checking
```

The front end has no tests. To iterate on C# without touching Node, build `Wistellar.Core` on its own:

```bash
dotnet build Wistellar.Core/Wistellar.Core.csproj
```

## Building the container image

```bash
docker build -t wistellar -f docker/Dockerfile .
docker run -d --name wistellar -p 8080:8080 -v wistellar_data:/app/data wistellar
```

## Repository layout

| Path | Role |
| --- | --- |
| `Wistellar.Core` | Data model, EF Core migrations, importers, GeoJSON and enrichment services |
| `Wistellar.Server` | ASP.NET Core API, authentication, user-management CLI |
| `Wistellar.Frontend` | SvelteKit + MapLibre web map |
| `Wistellar.Tests` | Server-side tests |
| `docker/` | Server image, plus the survey app build |
| `.github/workflows/` | CI builds and image publishing |

## Common changes

**A new import format.** Implement `ITextImport` in `Wistellar.Core/Import` and register it. The upload
pipeline offers every incoming CSV file to each importer in turn, and each one decides from the first
line whether the file is its format.

**A database change.** Use the EF Core tool pinned in
[Wistellar.Server/.config/dotnet-tools.json](../Wistellar.Server/.config/dotnet-tools.json); a globally
installed older version will fail.

```bash
cd Wistellar.Server && dotnet tool restore
dotnet ef migrations list --project ../Wistellar.Core --startup-project ../Wistellar.Core
dotnet ef migrations add <Name>  --project ../Wistellar.Core --startup-project ../Wistellar.Core
```

Point `--startup-project` at `Wistellar.Core`, not the server, so the tool never builds the server and
never runs pnpm. Migrations are applied automatically when the server starts.

**A new map filter.** Filters are compiled into one expression in
[GeoJsonNetworksService.cs](../Wistellar.Core/Services/GeoJsonNetworksService.cs). Every clause has the
form `value == null || ...` and uses `EF.Constant(...)` rather than parameters, so SQLite can use its
index on `network`. Follow the same pattern for new fields.

## Releasing

Pushes to `main` publish the `edge` image. To publish a release, push a `vMAJOR.MINOR.PATCH` tag:

```bash
git tag v1.2.3
git push origin v1.2.3
```

The version comes from the tag alone; nothing in the repository needs editing first. The release
build also copies the README to the Docker Hub overview.
