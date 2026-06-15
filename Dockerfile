# ── Stage 1: build ──────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

COPY SafeVault.sln .
COPY src/Domain/SafeVault.Domain.csproj               src/Domain/
COPY src/Application/SafeVault.Application.csproj     src/Application/
COPY src/Infrastructure/SafeVault.Infrastructure.csproj src/Infrastructure/
COPY src/InterfaceAdapters/SafeVault.InterfaceAdapters.csproj src/InterfaceAdapters/

RUN dotnet restore

COPY src/ src/

RUN dotnet publish src/InterfaceAdapters/SafeVault.InterfaceAdapters.csproj \
    --configuration Release \
    --no-restore \
    --output /publish

# ── Stage 2: runtime ────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Install curl for health checks, then clean up
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as non-root user
RUN addgroup --system safevault && adduser --system --ingroup safevault safevault

# Create storage and log directories owned by the app user
RUN mkdir -p /app/storage /app/logs && chown -R safevault:safevault /app

COPY --from=build --chown=safevault:safevault /publish .

USER safevault

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "SafeVault.InterfaceAdapters.dll"]
