# syntax=docker/dockerfile:1

# Both images are pinned by version and digest, so a rebuild gets the same toolchain and runtime until someone bumps
# them on purpose. global.json is not copied in: the SDK the image carries is the one it builds with.
FROM mcr.microsoft.com/dotnet/sdk:10.0.402@sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2 AS build
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

FROM mcr.microsoft.com/dotnet/aspnet:10.0.13@sha256:8af87eba5e7cdafb1514d7388be287365da97c132509e84dfe252ffd552398ca AS final
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
