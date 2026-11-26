# GameOptEngine

GameOptEngine is an independent .NET worker-service project in a multi-project repository. It watches configured or Steam-discovered game processes, records indexed activity, and serves a local activity viewer. It is detection-only: it does not terminate games, development tools, cloud agents, system processes, or other background work.

## Requirements

- Windows 10/11 for game-process and available-RAM monitoring
- .NET 8 SDK
- Docker Desktop with Compose (optional)

## Build and test

Run from this project directory:

```powershell
dotnet restore GameOptEngine.sln
dotnet build GameOptEngine.sln --configuration Release
dotnet test GameOptEngine.sln --configuration Release
```

## Run from a desktop shortcut

Publish a self-contained Windows build:

```powershell
dotnet publish src/GameOptEngine.Service/GameOptEngine.Service.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:UseAppHost=true --output publish/win-x64
```

Create a desktop shortcut targeting `publish\win-x64\GameOptEngine.Service.exe`. Set its **Start in** directory to the GameOptEngine project folder. The service watches game processes and serves the local activity viewer at <http://127.0.0.1:5088/>. Only one service instance can run at a time; launching the shortcut again prompts whether to open the existing activity viewer, then exits the duplicate launch. When the running service exits, its hosted monitor and web server shut down gracefully.

To create a shortcut that also opens the viewer in a browser on every launch, target:

```text
C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe
```

Use these arguments (adjust the project path as needed):

```text
-NoProfile -WindowStyle Hidden -Command "Start-Process -FilePath 'C:\path\to\GameOptEngine\publish\win-x64\GameOptEngine.Service.exe'; Start-Sleep -Seconds 3; Start-Process 'http://127.0.0.1:5088/'"
```

Prefer the direct service shortcut above if you want the single-instance prompt to control whether a browser opens. The application monitors only detected process names and logs game start/exit events with indexed sequence numbers. Configure extra game process names with `GAMEOPTENGINE_GAME_PROCESS_NAMES=GameA,GameB`. It does not inspect keystrokes, browser history, or general user activity.

## Colored command-line activity viewer

`Prettier` formats source code; it is not a terminal UI. The CLI uses the `Spectre.Console` .NET package for colored, indexed activity tables.

```powershell
dotnet run --project src/GameOptEngine.Cli
dotnet run --project src/GameOptEngine.Cli -- --follow
dotnet run --project src/GameOptEngine.Cli -- --log "$env:LOCALAPPDATA\GameOptEngine\activity.jsonl"
```

Build the CLI with `dotnet build src/GameOptEngine.Cli/GameOptEngine.Cli.csproj --configuration Release`.

## Trivy image scan

Install Trivy on Windows with winget, then build and scan the image:

```powershell
winget install --id AquaSecurity.Trivy --exact --source winget
docker build --tag gameoptengine:local .
./scripts/scan-image.ps1
```

The script scans the local image for vulnerabilities, misconfiguration, and embedded secrets, and fails the command for HIGH or CRITICAL findings. The GitHub Actions workflow also scans the built image. Review findings rather than suppressing them by default.

## Activity records

On Windows, the default JSON Lines file is `%LOCALAPPDATA%\GameOptEngine\activity.jsonl`. Override it with `GAMEOPTENGINE_ACTIVITY_LOG_PATH`. Each record includes a monotonically increasing `index`, UTC timestamp, activity type, and details. The HTML viewer refreshes periodically; `/api/activity` returns JSON.

## Docker Compose

```powershell
docker compose up --pull always --build -d
```

Open <http://127.0.0.1:5088/>. Follow the service logs with:

```powershell
docker compose logs -f gameoptengine
```

Stop it with:

```powershell
docker compose down
```

Compose persists activity records in the `gameoptengine-activity` volume. Docker cannot monitor Windows host processes from this Linux container; run the published Windows service for host game monitoring.

## Safety and scope

Process-wide termination is deliberately unsupported. Avoid “kill every process” behavior: it can destroy unsaved work, interrupt builds and deployments, stop security software, and crash Windows. GameOptEngine is a standalone project and does not claim to optimize unrelated projects or cloud environments.
