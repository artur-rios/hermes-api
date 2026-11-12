# syntax=docker/dockerfile:1

# Both images are pinned by version and digest, so a rebuild gets the same toolchain and runtime until someone bumps
# them on purpose. global.json is not copied in: the SDK the image carries is the one it builds with.
FROM mcr.microsoft.com/dotnet/sdk:10.0.404@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /source

# Restored on their own so the package layer survives a source-only change. Restore cannot evaluate the project
# without the central package versions and the shared build settings.
COPY Directory.Packages.props Directory.Build.shared.props ./
COPY src/Directory.Build.props src/
COPY src/ArturRios.Hermes.WebApi/ArturRios.Hermes.WebApi.csproj src/ArturRios.Hermes.WebApi/
RUN dotnet restore src/ArturRios.Hermes.WebApi/ArturRios.Hermes.WebApi.csproj

COPY src/ src/
RUN dotnet publish src/ArturRios.Hermes.WebApi/ArturRios.Hermes.WebApi.csproj \
        --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS final
WORKDIR /app

# For the container health check alone: the runtime image ships neither curl nor wget.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

USER $APP_UID

# 8080 is the API. 9464 is the Prometheus scrape, answered only on connections that arrive on it, and never published.
ENV ASPNETCORE_HTTP_PORTS=8080;9464
EXPOSE 8080 9464

ENTRYPOINT ["dotnet", "ArturRios.Hermes.WebApi.dll"]
