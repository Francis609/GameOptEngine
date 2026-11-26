# GameOptEngine Copilot Instructions

- This repository is a .NET 8 solution. Keep nullable reference types enabled.
- Preserve the solution layout: applications in `src/` and xUnit tests in `tests/`.
- Keep changes small, testable, and aligned with existing project boundaries.
- Add or update xUnit tests for behavior changes and run `dotnet test GameOptEngine.sln --configuration Release`.
- Run `dotnet build GameOptEngine.sln --configuration Release` before considering a change complete.
- Treat package vulnerability warnings as build blockers; do not suppress advisories without documented justification.
- The worker can run in Linux containers. Do not assume Windows APIs or host process access.
- Process termination must remain explicitly opt-in and must never target unrelated host processes.
- Do not add secrets to source, settings, Docker build arguments, or image layers. Use environment injection or a secret manager.
- Keep containers non-root and least-privileged; preserve the Compose read-only filesystem and dropped capabilities.
