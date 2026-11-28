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
- **Expressive filtering** — filter by network type, name, MAC address, capabilities, observation
  count, distance covered, dwell time and last-seen time from the map's filters pane. The filter is
  kept in the page URL, so a filtered map can be bookmarked or shared.
- **Several input formats** — see [Data sources](#data-sources). `.gz` and `.zip` archives are
  unpacked automatically.
- **Automatic enrichment** — MAC addresses are resolved to hardware vendors via the IEEE OUI registry,
  and cellular networks to operators via MCC/MNC data.
- **Self-contained** — a single SQLite file holds everything; no external database to run. The only
  outside services it talks to are the OpenFreeMap base map loaded by the browser, and the IEEE OUI and
  MCC/MNC lists downloaded once on first start.

## Data sources

### File import

Sign in and choose **Import files** from the map's menu to upload CSV, KML, KMZ, ZIP or SQLite files,
optionally GZip-compressed. Archives are unpacked on the server.
You can select several files, follow their progress and review any warnings. The map refreshes when
the import finishes.
The importer picks the format by content type or file extension (`.csv`, `.kml`, `.sqlite`, plus
`.gz`, `.zip` and `.kmz` wrappers), and tells the CSV formats apart by their first line.

| Format | Notes |
| --- | --- |
| WiGLE CSV | The `WigleWifi-1.x` observation logs exported by wardriving apps |
| WiGLE SQLite backup | A whole phone backup database, imported directly |
| wifidb KML / KMZ | Exports from wifidb.net; KMZ archives are unpacked on the server |
| OpenCellID CSV | Public cell-tower dataset |
| Mylnikov CSV | Public geolocation dataset |
| Network Survey CSV | The per-technology `craxiom-*.csv` logs (Wi-Fi, Bluetooth, LTE, NR, UMTS, GSM, CDMA) |

### Survey apps

| App | Status |
| --- | --- |
| [WiGLE WiFi Wardriving](https://github.com/wiglenet/wigle-wifi-wardriving) | **Supported** — see [Survey app setup](#survey-app-setup). |
| [Network Survey](https://github.com/christianrowlands/android-network-survey) | **Import only** — upload its CSV log files. |

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
