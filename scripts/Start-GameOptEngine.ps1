$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$binary = Join-Path $repoRoot 'target\release\gameoptengine.exe'
$address = 'http://127.0.0.1:5088/'

if (-not (Test-Path -LiteralPath $binary)) {
    throw "GameOptEngine binary not found at '$binary'. Build it first with: cargo build --release"
}

$existing = Get-NetTCPConnection -LocalPort 5088 -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalAddress -eq '127.0.0.1' -or $_.LocalAddress -eq '0.0.0.0' } |
    Select-Object -First 1

if (-not $existing) {
    Start-Process -FilePath $binary -ArgumentList 'run' -WorkingDirectory $repoRoot
    $deadline = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 500
        $existing = Get-NetTCPConnection -LocalPort 5088 -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $_.LocalAddress -eq '127.0.0.1' -or $_.LocalAddress -eq '0.0.0.0' } |
            Select-Object -First 1
    } while (-not $existing -and (Get-Date) -lt $deadline)
}

if (-not $existing) {
    throw 'GameOptEngine did not start listening on port 5088. Check the process output and activity log.'
}

Start-Process $address
