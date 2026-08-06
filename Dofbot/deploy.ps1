[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$Jetson = "jetson@192.168.137.179"
$Container = "kind_pare"

$LocalFile = Join-Path $PSScriptRoot "robot_api.py"
$LocalVisionFile = Join-Path $PSScriptRoot "vision_trigger.py"
$HostFile = "/home/jetson/robot_api.py"
$HostVisionFile = "/home/jetson/vision_trigger.py"
$ContainerFile = "/root/robot_api.py"
$ContainerVisionFile = "/root/vision_trigger.py"

function Assert-LastCommand {
    param([string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed. Exit code: $LASTEXITCODE"
    }
}

if (-not (Test-Path $LocalFile)) {
    throw "Không tìm thấy file: $LocalFile"
}

if (-not (Test-Path $LocalVisionFile)) {
    throw "Không tìm thấy file: $LocalVisionFile"
}

Write-Host "1. Uploading robot_api.py to Jetson..."
scp $LocalFile "${Jetson}:$HostFile"
Assert-LastCommand "Upload robot_api.py"

Write-Host "   Uploading vision_trigger.py to Jetson..."
scp $LocalVisionFile "${Jetson}:$HostVisionFile"
Assert-LastCommand "Upload vision_trigger.py"

Write-Host "2. Starting container..."
ssh $Jetson "docker start $Container >/dev/null 2>&1 || true"
Assert-LastCommand "Start container"

Start-Sleep -Seconds 1

Write-Host "3. Checking container status..."
$status = ssh $Jetson "docker inspect -f '{{.State.Status}}' $Container"
Assert-LastCommand "Read container status"

$status = $status.Trim()

if ($status -ne "running") {
    throw "Container $Container is not running. Status: $status"
}

Write-Host "Container status: running"

Write-Host "4. Writing file into container..."

ssh $Jetson "cat $HostFile | docker exec -i $Container sh -c 'cat > $ContainerFile'"
Assert-LastCommand "Write robot_api.py into container"

ssh $Jetson "cat $HostVisionFile | docker exec -i $Container sh -c 'cat > $ContainerVisionFile'"
Assert-LastCommand "Write vision_trigger.py into container"

Write-Host "5. Checking file..."
ssh $Jetson "docker exec $Container test -s $ContainerFile"
Assert-LastCommand "Check robot_api.py"

ssh $Jetson "docker exec $Container test -s $ContainerVisionFile"
Assert-LastCommand "Check vision_trigger.py"

Write-Host "6. Checking Python syntax..."
ssh $Jetson "docker exec $Container python3 -m py_compile $ContainerFile"
Assert-LastCommand "Python syntax check"

ssh $Jetson "docker exec $Container python3 -m py_compile $ContainerVisionFile"
Assert-LastCommand "Vision Python syntax check"

Write-Host "7. Showing deployed file information..."
ssh $Jetson "docker exec $Container ls -lh $ContainerFile"
Assert-LastCommand "Read deployed file information"

ssh $Jetson "docker exec $Container ls -lh $ContainerVisionFile"
Assert-LastCommand "Read deployed vision file information"

Write-Host ""
Write-Host "Deploy completed successfully."
