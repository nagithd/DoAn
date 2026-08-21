[CmdletBinding()]
param(
    [string]$Jetson = "jetson@192.168.137.179",
    [string]$JetsonIp = "192.168.137.179",
    [string]$Container = "dofbot_robot_api",
    [switch]$IncludeJetsonDiagnostics
)

$ErrorActionPreference = "Continue"
$TrtUri = "http://${JetsonIp}:7101"
$RobotUri = "http://${JetsonIp}:7000"

Write-Host "===== TensorRT monitor health ====="
try {
    $TrtHealth = Invoke-RestMethod `
        -Uri "$TrtUri/health" `
        -TimeoutSec 8
    $TrtHealth | ConvertTo-Json -Depth 10
    if (-not $TrtHealth.monitor_only) {
        Write-Warning "TensorRT sidecar is not marked monitor-only."
    }
}
catch {
    Write-Warning $_.Exception.Message
}

Write-Host "`n===== DOFBOT vision status ====="
try {
    $VisionStatus = Invoke-RestMethod `
        -Uri "$RobotUri/vision/status" `
        -TimeoutSec 8
    $VisionStatus | ConvertTo-Json -Depth 10

    Write-Host "`n===== Safety summary ====="
    Write-Host "monitor_only             : $($VisionStatus.monitor_only)"
    Write-Host "auto_trigger             : $($VisionStatus.auto_trigger)"
    Write-Host "vision_processing_enabled: $($VisionStatus.vision_processing_enabled)"
    Write-Host "camera_open              : $($VisionStatus.camera_open)"
    Write-Host "detector_available       : $($VisionStatus.detector_available)"
    Write-Host "last_inference_ms         : $($VisionStatus.last_inference_ms)"
    Write-Host "last_error                : $($VisionStatus.last_error)"

    if (-not $VisionStatus.monitor_only -or $VisionStatus.auto_trigger) {
        Write-Warning "Unsafe configuration: stop vision before continuing."
    }
    if (-not $VisionStatus.vision_processing_enabled) {
        Write-Warning (
            "YOLO processing is disabled. Use " +
            "install_dofbot_yolo_monitor.ps1 after camera diagnostics pass."
        )
    }
}
catch {
    Write-Warning $_.Exception.Message
}

if (-not $IncludeJetsonDiagnostics) {
    return
}

Write-Host "`n===== Jetson camera ownership and service details ====="
Write-Host (
    "The following read-only diagnostics may request the Jetson sudo " +
    "password once."
)

ssh -t $Jetson @"
echo '===== systemd services ====='
sudo systemctl --no-pager --full status dofbot-trt-inference.service || true
sudo systemctl --no-pager --full status dofbot-robot-api.service || true
echo
echo '===== host video device ====='
ls -l /dev/video* 2>/dev/null || true
sudo fuser -v /dev/video0 2>/dev/null || true
echo
echo '===== container video mapping ====='
sudo docker inspect -f '{{json .HostConfig.Devices}}' '$Container' || true
sudo docker exec '$Container' sh -c 'ls -l /dev/video* 2>/dev/null || true'
echo
echo '===== possible camera owners in running containers ====='
for container_id in `$(sudo docker ps -q); do
    container_name=`$(sudo docker inspect -f '{{.Name}}' "`$container_id")
    echo "--- `$container_name ---"
    sudo docker top "`$container_id" | grep -E 'robot_api.py|YahboomArm.pyc|vision_trigger|python3' || true
done
echo
echo '===== recent vision/TensorRT journal ====='
sudo journalctl -b -u dofbot-trt-inference.service -n 60 --no-pager || true
sudo journalctl -b -u dofbot-robot-api.service -n 80 --no-pager | grep -E 'vision|camera|video|TensorRT|ERROR|WARNING' || true
"@
