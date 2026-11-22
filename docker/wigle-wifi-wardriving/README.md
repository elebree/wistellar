# Building the WiGLE WiFi Wardriving app for Wistellar

The [WiGLE WiFi Wardriving](https://github.com/wiglenet/wigle-wifi-wardriving) Android app is a
well-established tool for wireless network mapping. It uploads to the public `wigle.net` server, and
that address is hardcoded — there is no setting for pointing it somewhere else.

This directory contains a Docker Compose build that clones the upstream app, rewrites the server URLs
to your own [Wistellar](../../README.md) instance, and produces an installable APK.

## Prerequisites

- **Docker** with **Docker Compose**
- A reachable URL for your Wistellar server
- Patience and disk space for the first run: it downloads the Android SDK, the app source and the
  Gradle dependencies before it can build anything

## Configuration

The build is driven entirely by environment variables.

| Variable | Required | Default | Purpose |
| --- | --- | --- | --- |
| `SERVER_URL` | **yes** | — | Base URL of your Wistellar server, e.g. `https://wistellar.example.com`. Replaces `https://wigle.net` throughout the app. |
| `API_HOST` | no | derived from `SERVER_URL` | API hostname, without the scheme. Replaces `api.wigle.net`. Set this only if your API lives on a different host from the web interface. |
| `GIT_TAG` | no | `foss-2.111` | Tag or branch of the upstream repository to build. See [Building a different version](#building-a-different-version). |
| `FOSS_MAP_STYLE_URL` | no | `https://tiles.openfreemap.org/styles/` | Preset for the app's *Map vector tile server style URL base* setting. The map loads its style from this value followed by `FOSS_MAP_KEY`; the default is OpenFreeMap, the same base map as Wistellar's web UI. A value saved in the app's settings wins. |
| `FOSS_MAP_KEY` | no | `liberty` | Preset for the app's *Map vector base key* setting. |
| `MAPS_API_KEY` | no | `undefined` | Google Maps API key. Only revisions outside the `foss-*` line, such as upstream `main`, use Google Maps; `foss-*` builds ignore it. |

## Build

Run from **this directory** — the Compose file uses it as the build context.

Linux / macOS:

```bash
export SERVER_URL="https://your.server.url"
docker compose run --rm build
```

Windows (PowerShell):

```powershell
$env:SERVER_URL = "https://your.server.url"
docker compose run --rm build
```

Windows (CMD):

```
set "SERVER_URL=https://your.server.url" && docker compose run --rm build
```

If you are on Compose v1, use `docker-compose` instead of `docker compose`.

## Installing the APK

The build writes the APK into `./output/debug/`:

```bash
ls ./output/debug/
adb install ./output/debug/<name>-debug.apk
```

The app is signed with Android's debug key, so your device will treat it as an app from an unknown
source and ask you to confirm the install.

Log in with a user created through Wistellar's
[user-management CLI](../../README.md#user-management) — see below for why.

### Replacing an earlier build

A new build cannot be installed over an earlier one. Each `docker compose run --rm` starts a fresh
container that signs the APK with a newly generated debug key, and Android only updates an app from
an APK signed with the same key. Remove the old build first:

1. Upload everything the app has collected. Uninstalling deletes the app's local database.
2. Uninstall the old build. Builds of `foss-2.107` and later use the package name
   `net.wigle.wigleandroid`; earlier `foss-*` builds use `net.wigle.wigleandroid.foss`:

   ```bash
   adb uninstall net.wigle.wigleandroid.foss   # for example, a foss-2.104 build
   ```

3. Install the new APK as above.

A build from here cannot be installed next to the WiGLE app from Google Play either, so uninstall
that one too.

## Building a different version

Upstream publishes two families of tags: `foss-*` builds and plain numeric ones. Pick one with
`GIT_TAG`:

```bash
export GIT_TAG="foss-2.104"
```

The build script patches whichever mapping class the chosen revision uses — the class was renamed
`MappingFragment` → `AbstractMappingFragment` in the `foss-*` line — and fails with a clear message if
it finds neither. If the tag does not exist, it falls back to the upstream default branch.

The package name depends on the tag: `foss-2.107` and later build `net.wigle.wigleandroid`, the same
name as the WiGLE app on Google Play; earlier `foss-*` tags build `net.wigle.wigleandroid.foss`.

> **The upstream clone is cached in `./source` and reused.** Changing `GIT_TAG` on a later run has no
> effect until you delete that directory:
>
> ```bash
> rm -rf ./source
> ```
>
> Changing `SERVER_URL` or the `FOSS_MAP_*` variables alone does not require this — the patching
> step re-runs every time.

## Limitations

- **In-app registration does not work.** The app's sign-up screen posts to an endpoint Wistellar does
  not implement. Create accounts with the
  [user-management CLI](../../README.md#user-management) instead.
- **The map's *Show Discovered Networks* overlay stays empty.** It requests tiles from `/clientTile`,
  which Wistellar does not serve. The base map and the networks the phone is collecting still show;
  use Wistellar's own web map to browse everything you have uploaded.

Uploading observations — the reason to build this app — is unaffected by both.

## Licence

This directory only contains build tooling. The app source is downloaded from
[wiglenet/wigle-wifi-wardriving](https://github.com/wiglenet/wigle-wifi-wardriving) at build time and
nothing from it is redistributed here, but the APK you get out is a derived work of that project.
Check its licence terms before sharing builds with anyone else.
