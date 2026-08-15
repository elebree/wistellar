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
| `MAPS_API_KEY` | no | `undefined` | Google Maps API key, used by the app's built-in map. Leave unset unless you need that map to work. |
| `GIT_TAG` | no | `foss-2.104` | Tag or branch of the upstream repository to build. See [Building a different version](#building-a-different-version). |

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

## Building a different version

Upstream publishes two families of tags: `foss-*` builds and plain numeric ones. Pick one with
`GIT_TAG`:

```bash
export GIT_TAG="foss-2.108"
```

The build script patches whichever mapping class the chosen revision uses — the class was renamed
`MappingFragment` → `AbstractMappingFragment` in the `foss-*` line — and fails with a clear message if
it finds neither. If the tag does not exist, it falls back to the upstream default branch.

> **The upstream clone is cached in `./source` and reused.** Changing `GIT_TAG` on a later run has no
> effect until you delete that directory:
>
> ```bash
> rm -rf ./source
> ```
>
> Changing `SERVER_URL` alone does not require this — the patching step re-runs every time.

## Limitations

- **In-app registration does not work.** The app's sign-up screen posts to an endpoint Wistellar does
  not implement. Create accounts with the
  [user-management CLI](../../README.md#user-management) instead.
- **The app's built-in map stays empty.** It requests tiles in a format Wistellar does not serve. Use
  Wistellar's own web map to browse what you have collected.

Uploading observations — the reason to build this app — is unaffected by both.

## Licence

This directory only contains build tooling. The app source is downloaded from
[wiglenet/wigle-wifi-wardriving](https://github.com/wiglenet/wigle-wifi-wardriving) at build time and
nothing from it is redistributed here, but the APK you get out is a derived work of that project.
Check its licence terms before sharing builds with anyone else.
