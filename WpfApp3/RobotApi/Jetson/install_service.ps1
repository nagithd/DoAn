param(
    [string]$Jetson = "jetson@192.168.137.179"
)

$ErrorActionPreference = "Stop"
$ServiceName = "dofbot-robot-api.service"
$LocalService = Join-Path $PSScriptRoot $ServiceName
$RemoteService = "/home/jetson/$ServiceName"

function Assert-LastCommand {
    param([string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed. Exit code: $LASTEXITCODE"
    }
}

if (-not (Test-Path -LiteralPath $LocalService)) {
    throw "Service file not found: $LocalService"
}

Write-Host "1. Uploading $ServiceName to Jetson..."
scp $LocalService "${Jetson}:$RemoteService"
Assert-LastCommand "Upload systemd service"

Write-Host "2. Installing and enabling the service..."
ssh -t $Jetson @"
sudo install -m 0644 '$RemoteService' '/etc/systemd/system/$ServiceName' &&
sudo systemctl daemon-reload &&
sudo systemctl enable '$ServiceName' &&
sudo systemctl restart '$ServiceName' &&
sudo systemctl --no-pager --full status '$ServiceName'
"@
Assert-LastCommand "Install systemd service"

Write-Host ""
Write-Host "Service installed successfully."
Write-Host "Robot API should start automatically after every Jetson boot."
Write-Host "Health URL: http://<JETSON-IP>:7000/health"
