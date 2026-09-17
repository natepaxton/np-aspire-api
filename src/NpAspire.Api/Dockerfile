# syntax=docker/dockerfile:1
# Build context: repository root (shared props, global.json, and ServiceDefaults live outside this folder).
#   docker build -f src/NpAspire.Api/Dockerfile -t np-aspire-api .

# ---- build -------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.400 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /repo

# Restore first, from project files only, so the layer is reused until dependencies change.
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/NpAspire.ServiceDefaults/NpAspire.ServiceDefaults.csproj src/NpAspire.ServiceDefaults/
COPY src/NpAspire.Api/NpAspire.Api.csproj src/NpAspire.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet restore src/NpAspire.Api/NpAspire.Api.csproj

COPY src/NpAspire.ServiceDefaults/ src/NpAspire.ServiceDefaults/
COPY src/NpAspire.Api/ src/NpAspire.Api/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages \
    dotnet publish src/NpAspire.Api/NpAspire.Api.csproj \
        --configuration "$BUILD_CONFIGURATION" \
        --no-restore \
        --output /app \
        -p:UseAppHost=false

# ---- runtime -----------------------------------------------------------------------------------------
# Chiseled: minimal Ubuntu, no shell or package manager, runs as the non-root `app` user (UID 1654).
FROM mcr.microsoft.com/dotnet/aspnet:10.0.12-noble-chiseled AS runtime
WORKDIR /app

# 8080: API traffic (reached through the gateway). 8081: management port for health checks only.
ENV ASPNETCORE_HTTP_PORTS=8080;8081 \
    HealthChecks__ManagementPort=8081
EXPOSE 8080 8081

COPY --from=build /app .

# No curl in a chiseled image; the API has a built-in probe mode.
HEALTHCHECK --interval=10s --timeout=5s --start-period=10s --retries=3 \
    CMD ["dotnet", "NpAspire.Api.dll", "--health-check", "http://127.0.0.1:8081/health"]

ENTRYPOINT ["dotnet", "NpAspire.Api.dll"]
