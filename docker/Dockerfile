# Builds the Wistellar server image, including the SvelteKit front end.
#
# Build from the repository root:
#   docker build -t wistellar-server -f docker/Dockerfile .
#
# Multi-architecture builds are cross-compiled rather than emulated, so they stay fast:
#   docker buildx build --platform linux/amd64,linux/arm64 -f docker/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

# curl is here only so HEALTHCHECK below has something to call the health endpoint with;
# the runtime image ships with neither curl nor wget.
RUN apt-get update -yq \
    && apt-get install -yq --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Pinned to the build machine's own architecture with $BUILDPLATFORM. The .NET output is
# cross-compiled for the target with `-a $TARGETARCH`, and the front end compiles to
# architecture-independent static files, so nothing here has to run under emulation.
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
# Supplied by BuildKit. This file needs BuildKit anyway for the cache mounts below, which
# every Docker 23+ engine uses by default; the value here is only a fallback.
ARG TARGETARCH=amd64
# pnpm honours this by defaulting `pnpm install` to --frozen-lockfile, so the image is built
# from the dependency versions CI type-checked rather than whatever resolves today.
ENV CI=true
# Kept in step with the versions CI uses (.github/workflows/ci.yml).
ARG NODE_MAJOR=20
ARG PNPM_VERSION=10.28.2

# The front end is built as part of the server project, so the image needs Node and pnpm.
RUN apt-get update -yq && apt-get install -yq --no-install-recommends curl ca-certificates && rm -rf /var/lib/apt/lists/*
RUN curl -fsSL https://deb.nodesource.com/setup_${NODE_MAJOR}.x | bash - \
    && apt-get install -yq --no-install-recommends nodejs \
    && rm -rf /var/lib/apt/lists/*
RUN npm install -g pnpm@${PNPM_VERSION}

WORKDIR /src

# Front-end dependencies first: they change far less often than the source, so this layer
# survives most rebuilds. node_modules then already exists by the time the .esproj runs its own
# `pnpm install` during publish, which turns that into a no-op.
COPY ["Wistellar.Frontend/package.json", "Wistellar.Frontend/pnpm-lock.yaml", "Wistellar.Frontend/.npmrc", "Wistellar.Frontend/"]
RUN --mount=type=cache,target=/root/.local/share/pnpm/store \
    cd Wistellar.Frontend && pnpm install --frozen-lockfile

# Every project the restore graph walks has to be here, or it is silently skipped and
# never restored - including the .esproj, which `dotnet publish --no-restore` then builds.
COPY ["Wistellar.Server/Wistellar.Server.csproj", "Wistellar.Server/"]
COPY ["Wistellar.Core/Wistellar.Core.csproj", "Wistellar.Core/"]
COPY ["Wistellar.Frontend/Wistellar.Frontend.esproj", "Wistellar.Frontend/"]
RUN dotnet restore "./Wistellar.Server/Wistellar.Server.csproj" -a $TARGETARCH

COPY . .
WORKDIR "/src/Wistellar.Server"
RUN --mount=type=cache,target=/root/.local/share/pnpm/store \
    dotnet publish "./Wistellar.Server.csproj" \
    -c $BUILD_CONFIGURATION \
    -a $TARGETARCH \
    --no-self-contained \
    --no-restore \
    -o /app/publish \
    /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .

# Holds the SQLite database and the generated JWT signing key. Mount this somewhere
# persistent, or data and issued tokens are lost when the container is replaced.
#
# Created and owned here so that a named volume mounted over it inherits the ownership,
# which is what lets the non-root user below write to it. Only this directory is chowned -
# the app never writes to the rest of /app, and a recursive chown would duplicate the whole
# publish output into another layer.
RUN mkdir -p /app/data && chown app:app /app/data
VOLUME ["/app/data"]

# The `app` user (uid 1654) ships with the .NET runtime images.
USER app

# The start period is generous because the first boot downloads the OUI and MCC-MNC tables
# before the app takes requests - a minute on a fast connection, longer on a Pi. Docker ends
# the start period early on the first success, so this costs nothing on later boots.
HEALTHCHECK --interval=30s --timeout=5s --start-period=300s --retries=3 \
    CMD curl -fsS http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Wistellar.Server.dll"]
