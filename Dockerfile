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
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /var/lib/gameoptengine && chown $APP_UID /var/lib/gameoptengine
USER $APP_UID
ENTRYPOINT ["dotnet", "GameOptEngine.Service.dll"]
