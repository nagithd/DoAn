[CmdletBinding()]
param(
    [string]$Jetson = "jetson@192.168.137.179",
    [string]$JetsonIp = "192.168.137.179",
    [string]$Container = "dofbot_robot_api",
    [string]$VisionSource = "",
    [int]$WaitSeconds = 30
)

$ErrorActionPreference = "Stop"
$RobotService = "dofbot-robot-api.service"
$PatchTool = Join-Path $PSScriptRoot "prepare_monitor_only_vision.py"
$GeneratedVision = Join-Path `
    ([System.IO.Path]::GetTempPath()) `
    "vision_trigger.monitor_only.generated.py"
$RemoteVision = "/home/jetson/vision_trigger.monitor_only.generated.py"

function Assert-LastCommand {
    param([string]$Step)
    if ($LASTEXITCODE -ne 0) {
        throw "$Step failed. Exit code: $LASTEXITCODE"
    }
}

if ([string]::IsNullOrWhiteSpace($VisionSource)) {
    $VisionSource = [System.IO.Path]::GetFullPath(
        (Join-Path $PSScriptRoot "..\Backend\vision_trigger.py"))
}

if (-not (Test-Path -LiteralPath $VisionSource)) {
    throw "Vision source was not found: $VisionSource"
}
if (-not (Test-Path -LiteralPath $PatchTool)) {
    throw "Monitor-only patch tool was not found: $PatchTool"
}

Write-Host "1. Verifying the inference-only TensorRT sidecar..."
$TrtHealth = Invoke-RestMethod `
    -Uri "http://${JetsonIp}:7101/health" `
    -TimeoutSec 10
if (-not $TrtHealth.success -or -not $TrtHealth.monitor_only) {
    throw "TensorRT sidecar is not healthy in monitor-only mode."
}
if ($TrtHealth.PSObject.Properties.Name -contains "routes_robot_motion") {
    if ($TrtHealth.routes_robot_motion) {
        throw "Refusing to continue: TensorRT sidecar reports motion routing."
    }
}

Write-Host "2. Verifying camera device mapping in $Container..."
ssh $Jetson "docker inspect '$Container' >/dev/null && docker exec '$Container' test -c /dev/video0"
Assert-LastCommand "Check /dev/video0 in $Container"

Write-Host "3. Generating a hard-locked monitor-only vision module..."
python $PatchTool $VisionSource $GeneratedVision
Assert-LastCommand "Generate monitor-only vision module"

Write-Host "4. Uploading and syntax-checking the generated module..."
scp $GeneratedVision "${Jetson}:$RemoteVision"
Assert-LastCommand "Upload generated vision module"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
ssh $Jetson @"
docker start '$Container' >/dev/null &&
docker exec '$Container' sh -c 'cp /root/vision_trigger.py /root/vision_trigger.before_yolo_monitor_$Timestamp.py' &&
cat '$RemoteVision' | docker exec -i '$Container' sh -c 'cat > /root/vision_trigger.py' &&
docker exec '$Container' python3 -m py_compile /root/vision_trigger.py
"@
Assert-LastCommand "Install generated vision module"

Write-Host "5. Restarting Robot API..."
ssh -t $Jetson `
    "sudo systemctl restart '$RobotService' && sudo systemctl --no-pager --full status '$RobotService'"
Assert-LastCommand "Restart $RobotService"

Write-Host "6. Enforcing monitor-only configuration through the API..."
$BaseUri = "http://${JetsonIp}:7000"
$Deadline = [DateTime]::UtcNow.AddSeconds([Math]::Max(10, $WaitSeconds))
$ApiReady = $false
do {
    try {
        Invoke-RestMethod -Uri "$BaseUri/health" -TimeoutSec 4 | Out-Null
        $ApiReady = $true
        break
    }
    catch {
        Start-Sleep -Seconds 1
    }
}
while ([DateTime]::UtcNow -lt $Deadline)
if (-not $ApiReady) {
    throw "Robot API did not become healthy at $BaseUri."
}

$ConfigBody = @{
    monitor_only = $true
    auto_trigger = $false
    detection_backend = "tensorrt_http"
    # dofbot_robot_api is created with --network host, so loopback reaches
    # the TensorRT sidecar on the Jetson host without Docker bridge routing.
    detector_url = "http://127.0.0.1:7101/infer"
    preview_jpeg_quality = 70
    preview_max_fps = 10
    # Cover the visible conveyor surface while excluding most of the bright
    # lower rail. These values also override an older persisted config file.
    entry_zone_center_x_ratio = 0.50
    entry_zone_center_y_ratio = 0.40
    entry_zone_width_ratio = 0.90
    entry_zone_height_ratio = 0.72
} | ConvertTo-Json
Invoke-RestMethod `
    -Uri "$BaseUri/vision/config" `
    -Method Post `
    -ContentType "application/json" `
    -Body $ConfigBody `
    -TimeoutSec 5 | Out-Null

Write-Host "7. Starting DOFBOT camera monitoring (no motion routing)..."
$Status = Invoke-RestMethod `
    -Uri "$BaseUri/vision/start" `
    -Method Post `
    -ContentType "application/json" `
    -Body "{}" `
    -TimeoutSec 8

do {
    if (
        $Status.monitor_only -and
        -not $Status.auto_trigger -and
        $Status.vision_processing_enabled -and
        $Status.camera_open -and
        $Status.detector_available
    ) {
        break
    }
    Start-Sleep -Seconds 1
    $Status = Invoke-RestMethod `
        -Uri "$BaseUri/vision/status" `
        -TimeoutSec 5
}
while ([DateTime]::UtcNow -lt $Deadline)

if (-not $Status.monitor_only -or $Status.auto_trigger) {
    Invoke-RestMethod `
        -Uri "$BaseUri/vision/stop" `
        -Method Post `
        -ContentType "application/json" `
        -Body "{}" `
        -TimeoutSec 5 | Out-Null
    throw "Safety assertion failed: vision is not monitor-only."
}
if (-not $Status.vision_processing_enabled) {
    throw "YOLO processing is still disabled in vision_trigger.py."
}
if (-not $Status.camera_open) {
    throw (
        "Monitor-only code is installed, but camera index 0 cannot open. " +
        "Run .\check_dofbot_yolo_monitor.ps1 -IncludeJetsonDiagnostics. " +
        "Last error: $($Status.last_error)"
    )
}
if (-not $Status.detector_available) {
    throw (
        "Camera is open, but the TensorRT detector did not answer. " +
        "Last error: $($Status.last_error)"
    )
}

$Status | ConvertTo-Json -Depth 10
Write-Host ""
Write-Host "DOFBOT YOLO monitor-only test is active."
Write-Host "No robot motion or auto-trigger endpoint was invoked."
Write-Host "MJPEG preview: $BaseUri/vision/stream.mjpg"
Write-Host "Raw JPEG fallback: $BaseUri/vision/frame.jpg"
Write-Host "Annotated JPEG: $BaseUri/vision/annotated.jpg"
