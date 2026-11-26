[CmdletBinding()]
param(
    [string]$Image = "gameoptengine:local",
    [switch]$InstallTrivy
)

$ErrorActionPreference = "Stop"

if (-not (Get-Command trivy -ErrorAction SilentlyContinue)) {
    if (-not $InstallTrivy) {
        throw "Trivy is not installed. Install it with: winget install --id AquaSecurity.Trivy --exact --source winget, then rerun this script."
    }

    if (-not (Get-Command winget -ErrorAction SilentlyContinue)) {
        throw "winget was not found. Install Trivy using the official Aqua Security installation instructions, then rerun this script."
    }

    winget install --id AquaSecurity.Trivy --exact --source winget
    if ($LASTEXITCODE -ne 0) {
        throw "winget could not install Trivy (exit code $LASTEXITCODE). Install it from https://trivy.dev/latest/getting-started/installation/ and rerun this script."
    }

    if (-not (Get-Command trivy -ErrorAction SilentlyContinue)) {
        throw "Trivy was installed, but is not on this shell's PATH. Open a new terminal and rerun this script."
    }
}

trivy image --scanners vuln,misconfig,secret --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 $Image
if ($LASTEXITCODE -ne 0) {
    throw "Trivy reported findings or failed to scan image '$Image' (exit code $LASTEXITCODE). Review the findings before release."
}
