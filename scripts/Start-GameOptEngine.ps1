$ErrorActionPreference = "Stop"
$service = Join-Path $PSScriptRoot "..\publish\win-x64\GameOptEngine.Service.exe"
$service = [System.IO.Path]::GetFullPath($service)
$address = "http://127.0.0.1:5088/"

if (-not (Test-Path -LiteralPath $service)) {
    throw "Published GameOptEngine service not found at '$service'. Publish the Windows service first."
}

$existing = Get-NetTCPConnection -LocalPort 5088 -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalAddress -eq "127.0.0.1" -or $_.LocalAddress -eq "0.0.0.0" } |
    Select-Object -First 1

if (-not $existing) {
    Start-Process -FilePath $service -WorkingDirectory (Split-Path $service)
    $deadline = (Get-Date).AddSeconds(15)
    do {
        Start-Sleep -Milliseconds 500
        $existing = Get-NetTCPConnection -LocalPort 5088 -State Listen -ErrorAction SilentlyContinue |
            Where-Object { $_.LocalAddress -eq "127.0.0.1" -or $_.LocalAddress -eq "0.0.0.0" } |
            Select-Object -First 1
    } while (-not $existing -and (Get-Date) -lt $deadline)
}

if (-not $existing) {
    throw "GameOptEngine did not start listening on port 5088. Check the activity log and any existing listener."
}

Start-Process $address
