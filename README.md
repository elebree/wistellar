# Wistellar

[![CI](https://github.com/elebree/wistellar/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/elebree/wistellar/actions/workflows/ci.yml)
[![Docker image](https://github.com/elebree/wistellar/actions/workflows/docker-publish.yml/badge.svg)](https://github.com/elebree/wistellar/actions/workflows/docker-publish.yml)
[![Release](https://img.shields.io/github/v/tag/elebree/wistellar?sort=semver&label=release)](https://github.com/elebree/wistellar/tags)
[![Docker Pulls](https://img.shields.io/docker/pulls/elebree/wistellar)](https://hub.docker.com/r/elebree/wistellar)
[![Image size](https://img.shields.io/docker/image-size/elebree/wistellar/latest)](https://hub.docker.com/r/elebree/wistellar/tags)
[![Licence: MIT](https://img.shields.io/github/license/elebree/wistellar)](LICENSE)

**A self-hosted server for collecting, storing and mapping wireless network surveys.**

Wistellar is a private home for wireless network observations — WiFi, Bluetooth and cellular. Point a
survey app at it, or import logs and public datasets you already have, and explore everything on an
interactive map. Your survey data stays in a database on your own server.

## Features

- **One map for every source** — WiFi, Bluetooth and cellular observations land in a single database
  and render as separate layers on a MapLibre map backed by vector tiles generated on the fly.
- **Expressive filtering** — filter by SSID, BSSID, capabilities, network type, observation count,
  signal range, dwell time and last-seen time, straight from the map's query box.
- **Several input formats** — see [Data sources](#data-sources). `.gz` and `.zip` archives are
  unpacked automatically.
- **Automatic enrichment** — MAC addresses are resolved to hardware vendors via the IEEE OUI registry,
  and cellular networks to operators via MCC/MNC data.
- **Self-contained** — a single SQLite file holds everything; no external database to run. The only
  outside services it talks to are the OpenFreeMap base map loaded by the browser, and the IEEE OUI and
  MCC/MNC lists downloaded once on first start.

## Data sources

### File import

File import is API-only: the web interface has no upload form. See [HTTP API](#http-api) for how to
upload with `curl` or any other HTTP client. The importer picks the format by content type or file
extension (`.csv`, `.kml`, `.sqlite`, plus `.gz` and `.zip` wrappers), and tells the CSV formats apart
by their first line.

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
| [WiGLE WiFi Wardriving](https://github.com/wiglenet/wigle-wifi-wardriving) | **Supported** — see [Survey app setup](#survey-app-setup). |

## Quick start with Docker

Prebuilt images for `linux/amd64` and `linux/arm64` are published to
[Docker Hub](https://hub.docker.com/r/elebree/wistellar) and the
[GitHub Container Registry](https://github.com/elebree/wistellar/pkgs/container/wistellar):

```bash
docker run -d --name wistellar -p 8080:8080 -v wistellar_data:/app/data --restart unless-stopped elebree/wistellar
```

The web interface is then at <http://localhost:8080>. The `/app/data` volume holds the SQLite database
and the generated JWT signing key — mount it somewhere persistent, or you will lose your data and
invalidate every issued token when the container is recreated.

To upgrade, pull the new image, remove the container and run the command above again; the data volume
carries over:

```bash
docker pull elebree/wistellar
docker rm -f wistellar
```

### Create the first user

There is no self-registration, so create your admin account before logging in:

```bash
docker exec -it wistellar dotnet Wistellar.Server.dll --add-user --username admin --password YourStrongPassword --role admin
```

## User management

Every user command runs the same way as the one above.

| Command | Arguments | Effect |
| --- | --- | --- |
| `--add-user` | `--username`, `--password`, `--role` *(optional)* | Creates a user. Fails if the name is taken. |
| `--update-user` | `--username`, `--password` *(optional)*, `--role` *(optional)* | Changes the password, the role, or both. |
| `--delete-user` | `--username` | Removes the user. |

Roles are `member` (the default), `moderator`, `contributor` and `admin`.

## Survey app setup

Wistellar implements the API the WiGLE Android app uploads to, but the app itself has `wigle.net`
hardcoded and offers no setting to point it elsewhere — so it has to be rebuilt against your server.
A Docker Compose build in this repository does that for you: it clones the upstream app, rewrites the
server URLs and produces an installable APK.

**→ [Building the WiGLE app for Wistellar](docker/wigle-wifi-wardriving/README.md)**

If you would rather not rebuild an app, collect to a file and use [file import](#file-import) instead.

## Configuration

Settings live under the `Wistellar` section of `appsettings.json`:

| Setting | Default | Meaning |
| --- | --- | --- |
| `ConnectionString` | `./data/wistellar.sqlite` | Path to the SQLite database file. Created and migrated on startup. |
| `IssuerSigningKeyFilePath` | `./data/issuer_signing_key.txt` | JWT signing key. Generated on first run and reused afterwards. |

Both can be overridden with environment variables using ASP.NET Core's double-underscore syntax, for
example `-e Wistellar__ConnectionString=/app/data/my-networks.sqlite` on the `docker run` command.

## Map filters

The map's query box, the tile API and the browser URL all share one filter syntax:

```
type=W|E|B&ssid=cafe_%&time[gt]=7d&locations[gt]=5
```

| Parameter | Notes |
| --- | --- |
| `type` | Network type letters, `\|`-separated. `W` WiFi, `B` Bluetooth, `E` BLE, `F` NFC, `G` GSM, `C` CDMA, `L` LTE, `D` UMTS, `N` 5G NR |
| `ssid`, `bssid` | SQL `LIKE` patterns (`%` and `_` wildcards), `\|`-separated for alternatives |
| `cap` | A single SQL `LIKE` pattern matched against the capabilities string |
| `range[gt]`, `range[lt]` | Distance between the farthest-apart observations, in metres |
| `locations[gt]`, `locations[lt]` | Number of recorded observations |
| `dwell[gt]`, `dwell[lt]` | Time between first and last observation, as a duration (`30m`, `12h`, `7d`) |
| `time[gt]`, `time[lt]` | Last seen. Accepts relative offsets (`30m`, `12h`, `7d`, `3M`, `1y`), ISO dates, or Unix seconds |

`range`, `locations` and `dwell` are recalculated only when the WiGLE app checks its upload status
after an upload. Data added through [file import](#file-import) alone is not reflected in these three
filters until that happens.

## HTTP API

Every endpoint except `activate` needs the token in an `Authorization: Bearer <token>` header.

| Endpoint | Purpose |
| --- | --- |
| `POST /api/v2/activate` | Sign in. Form fields `credential_0` (user name), `credential_1` (password) and `type`; returns a JWT valid for one month |
| `POST /api/v2/file/upload` | [File import](#file-import). Send the files as `multipart/form-data`; several files per request are accepted |
| `GET /geo/network` | Networks as a GeoJSON `FeatureCollection`. Accepts every [map filter](#map-filters), plus a bounding box: `lat[gt]`, `lat[lt]`, `lon[gt]`, `lon[lt]` |
| `GET /geo/location` | Individual observations as GeoJSON points. Filter by `bssid` (`\|`-separated) and `altitude[gt]` / `altitude[lt]` |

```bash
# Sign in and keep the token
TOKEN=$(curl -s -X POST http://localhost:8080/api/v2/activate -d credential_0=admin -d credential_1=YourStrongPassword -d type=device | jq -r .token)

# Import files
curl -X POST http://localhost:8080/api/v2/file/upload -H "Authorization: Bearer $TOKEN" -F file=@WigleWifi_20240101.csv -F file=@export.kml.gz

# Export filtered networks as GeoJSON
curl -G http://localhost:8080/geo/network -H "Authorization: Bearer $TOKEN" --data-urlencode 'type=W' --data-urlencode 'time[gt]=30d' > networks.geojson
```

A `/geo/network` request without a bounding box returns every matching network in the database.

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

## Development

Building from source, running locally and the project layout are covered in
[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Licence

MIT — see [LICENSE](LICENSE).
