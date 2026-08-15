# Wistellar

**A self-hosted server for collecting, storing and mapping wireless network surveys.**

Wistellar is a private home for wireless network observations — WiFi, Bluetooth and cellular. Point a
survey app at it, or import logs and public datasets you already have, and explore everything on an
interactive map. Nothing leaves your machine.

It is not tied to any single app or data format: observations arrive either as **file imports** in a
range of formats, or as **live uploads** from a survey app, and both feed the same database and the
same map.

## Features

- **One map for every source** — WiFi, Bluetooth and cellular observations land in a single database
  and render as separate layers on a MapLibre map backed by vector tiles generated on the fly.
- **Expressive filtering** — filter by SSID, BSSID, capabilities, network type, observation count,
  signal range, dwell time and last-seen time, straight from the map's query box.
- **Several input formats** — see [Data sources](#data-sources). Archives (`.gz`, `.zip`) are unpacked
  automatically, so you can hand it a whole export without unpacking first.
- **Automatic enrichment** — MAC addresses are resolved to hardware vendors via the IEEE OUI registry,
  and cellular networks to operators via MCC/MNC data.
- **Self-contained** — a single SQLite file holds everything; no external database to run.

## Data sources

### File import

Upload through the web interface or an API client. Detection is automatic — the importer inspects each
file rather than trusting its extension.

| Format | Notes |
| --- | --- |
| WiGLE CSV | The `WigleWifi-1.x` observation logs exported by wardriving apps |
| WiGLE SQLite backup | A whole phone backup database, imported directly |
| wifidb KML | Exports from wifidb.net |
| OpenCellID CSV | Public cell-tower dataset |
| Mylnikov CSV | Public geolocation dataset |

### Survey apps

| App | Status |
| --- | --- |
| [WiGLE WiFi Wardriving](https://github.com/wiglenet/wigle-wifi-wardriving) | **Supported** — Wistellar implements the API the app uploads to. See [Survey app setup](#survey-app-setup). |
| [Network Survey](https://github.com/christianrowlands/android-network-survey) | **Planned** |

Support for further formats and apps is on the [roadmap](#roadmap).

## Quick start with Docker

```bash
docker build -t wistellar-server -f docker/Dockerfile .

docker run -d --name wistellar \
  -p 8080:8080 \
  -v wistellar_data:/app/data \
  wistellar-server
```

The web interface is then at <http://localhost:8080>. The `/app/data` volume holds the SQLite database
and the generated JWT signing key — mount it somewhere persistent, or you will lose your data and
invalidate every issued token on the next rebuild.

### Create the first user

There is no self-registration. The server binary doubles as a user-management CLI, so create your admin
account before logging in:

```bash
docker exec -it wistellar dotnet Wistellar.Server.dll \
  --add-user --username admin --password 'YourStrongPassword' --role admin
```

## User management

Passing any argument to the server binary puts it into CLI mode: it runs the command and exits instead
of starting the web host. Run it through `docker exec` for a container, or with
`dotnet run --project Wistellar.Server --` from a source checkout.

| Command | Arguments | Effect |
| --- | --- | --- |
| `--add-user` | `--username`, `--password`, `--role` *(optional)* | Creates a user. Fails if the name is taken. |
| `--update-user` | `--username`, `--password` *(optional)*, `--role` *(optional)* | Changes the password, the role, or both. |
| `--delete-user` | `--username` | Removes the user. |

Roles are `member` (the default), `moderator`, `contributor` and `admin`.

```bash
# From a source checkout
dotnet run --project Wistellar.Server -- --add-user --username alice --password 'S3cret!' --role member
dotnet run --project Wistellar.Server -- --update-user --username alice --role moderator
dotnet run --project Wistellar.Server -- --delete-user --username alice
```

## Survey app setup

Wistellar implements the API the WiGLE Android app uploads to, but the app itself has `wigle.net`
hardcoded and offers no setting to point it elsewhere — so it has to be rebuilt against your server.
[docker/wigle-wifi-wardriving/](docker/wigle-wifi-wardriving/) contains a Docker Compose build that does
that for you: it clones the upstream app, rewrites the server URLs, and produces an installable APK.

**→ [Building the WiGLE app for Wistellar](docker/wigle-wifi-wardriving/README.md)**

If you would rather not rebuild an app, collect to a file and use [file import](#file-import) instead.

## Configuration

Settings live under the `Wistellar` section of `appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `ConnectionString` | `./data/wistellar.sqlite` | Path to the SQLite database file. Created and migrated on startup. |
| `IssuerSigningKeyFilePath` | `./data/issuer_signing_key.txt` | JWT signing key. Generated on first run and reused afterwards. |

Both can be overridden with environment variables using ASP.NET Core's double-underscore syntax:

```bash
docker run -d --name wistellar \
  -p 8080:8080 \
  -v wistellar_data:/app/data \
  -e "Wistellar__ConnectionString=/app/data/my-networks.sqlite" \
  wistellar-server
```

## Map filters

The map's query box, the tile API and the browser URL all share one filter syntax:

```
type=W|E|B&ssid=cafe_%&time[gt]=7d&locations[gt]=5
```

| Parameter | Notes |
| --- | --- |
| `type` | Network type letters, `\|`-separated. `W` WiFi, `B` Bluetooth, `E` BLE, `F` NFC, `G` GSM, `C` CDMA, `L` LTE, `D` UMTS, `N` 5G NR |
| `ssid`, `bssid`, `cap` | SQL `LIKE` patterns (`%` and `_` wildcards), `\|`-separated for alternatives |
| `range[gt]`, `range[lt]` | Estimated coverage radius, in metres |
| `locations[gt]`, `locations[lt]` | Number of recorded observations |
| `dwell[gt]`, `dwell[lt]` | Total observed duration |
| `time[gt]`, `time[lt]` | Last seen. Accepts relative offsets (`30m`, `12h`, `7d`, `3M`, `1y`), ISO dates, or Unix seconds |

## Roadmap

- Support for the [Network Survey](https://github.com/christianrowlands/android-network-survey) app.
- Additional import formats.

## Building from source

Requires the **.NET 9 SDK**, **Node.js 20+** and **pnpm**.

```bash
# API + web UI (building the server also builds the front end)
dotnet run --project Wistellar.Server
```

The API listens on <https://localhost:7188>, with Swagger at `/swagger` in Development.

To work on the front end with hot reload, run the server as above and start Vite alongside it — it
proxies `/api` and `/geo` through to the running API:

```bash
cd Wistellar.Frontend
pnpm install
pnpm run dev      # https://localhost:5173
```

### Layout

| Project | Role |
| --- | --- |
| `Wistellar.Core` | Data model, EF Core migrations, importers, GeoJSON and enrichment services |
| `Wistellar.Server` | ASP.NET Core API, authentication, user-management CLI |
| `Wistellar.Frontend` | SvelteKit + MapLibre web map |
| `docker/` | Server image, plus the survey app build |

New import formats are added by implementing `ITextImport` in `Wistellar.Core/Import` and registering
it — the upload pipeline then offers every incoming file to each importer in turn.

For the data model, the import pipeline, the filter DSL and the reasoning behind the less obvious
choices, see **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)**.

## Troubleshooting

**Container won't start** — check the logs:

```bash
docker logs wistellar
```

**SQLite cannot open the database file** — almost always a permissions problem on the mounted data
directory. Confirm what the container sees:

```bash
docker exec -it wistellar ls -la /app/data
```

Using a named volume (`-v wistellar_data:/app/data`) rather than a bind mount avoids most host
permission mismatches.

**Forgotten admin password** — reset it from the host:

```bash
docker exec -it wistellar dotnet Wistellar.Server.dll \
  --update-user --username admin --password 'NewStrongPassword'
```

## Licence

MIT — see [LICENSE](LICENSE).
