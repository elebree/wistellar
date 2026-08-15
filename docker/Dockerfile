# Builds the Wistellar server image, including the SvelteKit front end.
# Build from the repository root:
#   docker build -t wistellar-server -f docker/Dockerfile .

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release

# The front end is built as part of the server project, so the image needs Node and pnpm.
RUN apt-get update -yq && apt-get install -yq --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
RUN curl -sL https://deb.nodesource.com/setup_20.x | bash - && apt-get install -yq nodejs build-essential
RUN npm install -g pnpm

WORKDIR /src
COPY ["Wistellar.Server/Wistellar.Server.csproj", "Wistellar.Server/"]
COPY ["Wistellar.Core/Wistellar.Core.csproj", "Wistellar.Core/"]
RUN dotnet restore "./Wistellar.Server/Wistellar.Server.csproj"
COPY . .
WORKDIR "/src/Wistellar.Server"
RUN dotnet build "./Wistellar.Server.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Wistellar.Server.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Holds the SQLite database and the generated JWT signing key. Mount this somewhere
# persistent, or data and issued tokens are lost when the container is replaced.
VOLUME ["/app/data"]

ENTRYPOINT ["dotnet", "Wistellar.Server.dll"]
