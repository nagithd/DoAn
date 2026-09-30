[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
$ErrorActionPreference = "Stop"

$Jetson = "jetson@192.168.137.179"
$Container = "dofbot_robot_api"
$ServiceName = "dofbot-robot-api.service"
$HostDeployDirectory = "/home/jetson/dofbot_robot_api_deploy"
$ContainerDirectory = "/root"

$BackendFileNames = @(
    "robot_api.py",
    "robot_config.py",
    "robot_state.py",
    "robot_motion.py",
    "robot_jobs.py",
    "robot_routes.py",
    "vision_trigger.py",
    "pick_guard.py"
)

function Assert-LastCommand {
    param([string]$Step)

    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed. Exit code: $LASTEXITCODE"
    }
}

$LocalFiles = foreach ($fileName in $BackendFileNames) {
    $path = Join-Path $PSScriptRoot $fileName
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Không tìm thấy file backend: $path"
    }
    $path
}

Write-Host "1. Preparing Jetson staging directory..."
ssh $Jetson "mkdir -p '$HostDeployDirectory'"
Assert-LastCommand "Prepare Jetson staging directory"

Write-Host "2. Uploading Robot API modules..."
scp $LocalFiles "${Jetson}:$HostDeployDirectory/"
Assert-LastCommand "Upload Robot API modules"

Write-Host "3. Starting the fixed Robot API container..."
ssh $Jetson "docker start '$Container' >/dev/null 2>&1 || true"
Assert-LastCommand "Start container"

Start-Sleep -Seconds 1

$status = ssh $Jetson "docker inspect -f '{{.State.Status}}' '$Container'"
Assert-LastCommand "Read container status"

if ($status.Trim() -ne "running") {
    throw "Container $Container is not running. Status: $status"
}

Write-Host "4. Installing modules into /root inside the container..."
foreach ($fileName in $BackendFileNames) {
    $hostPath = "$HostDeployDirectory/$fileName"
    $containerPath = "$ContainerDirectory/$fileName"
    ssh $Jetson (
        "cat '$hostPath' | docker exec -i '$Container' " +
        "sh -c 'cat > `"$containerPath`"'"
    )
    Assert-LastCommand "Install $fileName into container"
}

Write-Host "5. Checking deployed files and Python syntax..."
$compileTargets = (
    $BackendFileNames |
        ForEach-Object { "$ContainerDirectory/$_" }
) -join " "

ssh $Jetson (
    "docker exec '$Container' python3 -m py_compile $compileTargets"
)
Assert-LastCommand "Python syntax check"

$importScript = @'
import pick_guard
import robot_config
import robot_state
import robot_motion
import robot_jobs
import robot_routes
import vision_trigger
'@

# Send Python through stdin. This avoids PowerShell -> SSH -> sh -> Python
# quote handling, which previously reduced the command to a bare `import`.
$importScript | ssh $Jetson (
    "docker exec -i '$Container' sh -c 'cd /root && python3 -'"
)
Assert-LastCommand "Python module import check"

ssh $Jetson (
    "docker exec '$Container' ls -lh $compileTargets"
)
Assert-LastCommand "Read deployed file information"

Write-Host "6. Restarting Robot API service..."
ssh -tt $Jetson (
    "sudo systemctl restart '$ServiceName' && " +
    "sleep 4 && " +
    "sudo systemctl --no-pager --full status '$ServiceName'"
)
Assert-LastCommand "Restart Robot API service"

Write-Host "7. Checking Robot API health..."
ssh $Jetson "curl -fsS http://127.0.0.1:7000/health"
Assert-LastCommand "Robot API health check"

Write-Host ""
Write-Host "Deploy completed successfully."
Write-Host "Container: $Container"
Write-Host "Service:   $ServiceName"
Write-Host "Health:    http://192.168.137.179:7000/health"
