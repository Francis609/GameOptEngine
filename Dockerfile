# syntax=docker/dockerfile:1.7
FROM mcr.microsoft.com/dotnet/sdk:8.0-noble AS build
WORKDIR /src

# Restore separately so source edits do not invalidate dependency layers.
COPY GameOptEngine.sln ./
COPY Directory.Build.props ./
COPY src/GameOptEngine.Service/GameOptEngine.Service.csproj src/GameOptEngine.Service/
RUN dotnet restore src/GameOptEngine.Service/GameOptEngine.Service.csproj

COPY src/GameOptEngine.Service/ src/GameOptEngine.Service/
RUN dotnet publish src/GameOptEngine.Service/GameOptEngine.Service.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false \
    && mkdir -p /var/lib/gameoptengine \
    && chown -R 1654:1654 /var/lib/gameoptengine

# Chiseled runtime removes package-manager and shell payload from the final image.
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app
RUN apk update \
    && apk upgrade --no-cache libcrypto3 libssl3
COPY --from=build /app/publish .
COPY --from=build --chown=1654:1654 /var/lib/gameoptengine /var/lib/gameoptengine
USER 1654:1654
ENTRYPOINT ["dotnet", "GameOptEngine.Service.dll"]
