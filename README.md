# GameOptEngine

GameOptEngine is a Rust process monitor and local activity viewer. It watches configured process names, records start/exit events and working-set memory, and never terminates games or unrelated processes. It does not free RAM in other processes; its own memory use is kept small by the Rust implementation.

## Requirements

- Rust toolchain and Cargo
- Windows for host game monitoring and Steam executable discovery
- Docker Desktop with Compose (optional; Linux containers cannot inspect Windows host processes)

## Build and test

```powershell
cargo fmt --all -- --check
cargo test --locked
cargo build --release --locked
```

## Run on Windows

Configure optional executable names, then run the monitor and viewer:

```powershell
$env:GAMEOPTENGINE_GAME_PROCESS_NAMES = "GameA,GameB"
cargo run --release
```

Open <http://127.0.0.1:5088/> for the HTML viewer or `/api/activity` for JSON. The JSONL activity file defaults under `%LOCALAPPDATA%\GameOptEngine\activity.jsonl`; override it with `GAMEOPTENGINE_ACTIVITY_LOG_PATH`.

Modes: `cargo run --release -- watch` monitors only, `cargo run --release -- serve` runs only the viewer, and `cargo run --release -- log` displays records. The default `run` mode starts both and stops when the process receives Ctrl+C. Publish a self-contained Windows executable with `cargo build --release --target x86_64-pc-windows-msvc`; the binary is `target\x86_64-pc-windows-msvc\release\gameoptengine.exe`.

## Docker Compose

```powershell
docker compose up --pull always --build -d
docker compose logs -f gameoptengine
docker compose down
```

Compose provides a viewer and persistent log volume. For game-process monitoring, run the Windows binary on the host; the Linux container cannot inspect host Windows processes.
