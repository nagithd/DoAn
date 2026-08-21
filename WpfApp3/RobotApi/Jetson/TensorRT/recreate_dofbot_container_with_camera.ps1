[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "High")]
param(
    [string]$Jetson = "jetson@192.168.137.179",
    [string]$Container = "dofbot_robot_api",
    [string]$DeployScript = ""
)

$ErrorActionPreference = "Stop"
$RobotService = "dofbot-robot-api.service"
$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupContainer = "${Container}_without_camera_${Timestamp}"

function Assert-LastCommand {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed. Exit code: $LASTEXITCODE"
    }
}

if ([string]::IsNullOrWhiteSpace($DeployScript)) {
    $DeployScript = [System.IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot "..\Backend\deploy.ps1"))
}
if (-not (Test-Path -LiteralPath $DeployScript)) {
    throw "DOFBOT deployment script was not found: $DeployScript"
}

$Description = (
    "Stop $RobotService, rename $Container to $BackupContainer, " +
    "and create a replacement with /dev/i2c-1 and /dev/video0"
)
if (-not $PSCmdlet.ShouldProcess($Jetson, $Description)) {
    return
}

Write-Host "1. Recreating the container with a recoverable backup..."
ssh -t $Jetson @"
set -e
test -c /dev/i2c-1
test -c /dev/video0
sudo systemctl stop '$RobotService'
image=`$(sudo docker inspect -f '{{.Config.Image}}' '$Container')
sudo docker stop '$Container' >/dev/null 2>&1 || true
sudo docker rename '$Container' '$BackupContainer'
if ! sudo docker create \
    --name '$Container' \
    --network host \
    --device /dev/i2c-1:/dev/i2c-1 \
    --device /dev/video0:/dev/video0 \
    --restart no \
    -e TZ=Asia/Ho_Chi_Minh \
    -e PYTHONUNBUFFERED=1 \
    --entrypoint /bin/bash \
    "`$image" \
    -lc 'sleep infinity' >/dev/null; then
    sudo docker rename '$BackupContainer' '$Container'
    echo 'Container creation failed; original name was restored.' >&2
    exit 1
fi
sudo docker start '$Container' >/dev/null
sudo docker exec '$Container' test -c /dev/i2c-1
sudo docker exec '$Container' test -c /dev/video0
sudo docker inspect -f 'Devices={{json .HostConfig.Devices}}' '$Container'
"@
Assert-LastCommand "Recreate $Container"

Write-Host "2. Deploying the current Robot API and vision sources..."
& $DeployScript
Assert-LastCommand "Deploy Robot API sources"

Write-Host "3. Starting Robot API service..."
ssh -t $Jetson `
    "sudo systemctl start '$RobotService' && sudo systemctl --no-pager --full status '$RobotService'"
Assert-LastCommand "Start $RobotService"

Write-Host ""
Write-Host "Replacement container created: $Container"
Write-Host "Recoverable old container: $BackupContainer"
Write-Host (
    "After verifying the Robot API and camera, keep the backup until the " +
    "test is complete. Do not run the Yahboom application concurrently."
)
