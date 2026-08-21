[CmdletBinding()]
param(
    [string]$Jetson = "jetson@192.168.137.179",
    [int]$WaitSeconds = 60
)

$ErrorActionPreference = "Stop"
$ServiceName = "dofbot-trt-inference.service"
$ScriptPath = Join-Path $PSScriptRoot "dofbot_trt_inference.py"
$UnitPath = Join-Path $PSScriptRoot $ServiceName

if (-not (Test-Path -LiteralPath $ScriptPath)) {
    throw "Missing TensorRT service script: $ScriptPath"
}
if (-not (Test-Path -LiteralPath $UnitPath)) {
    throw "Missing systemd unit: $UnitPath"
}

Write-Host "1. Checking Jetson host dependencies..."
ssh $Jetson "python3 -c 'import cv2,numpy,tensorrt'"
if ($LASTEXITCODE -ne 0) {
    throw "Jetson host is missing a required dependency. Do not install random TensorRT wheels; inspect the import error first."
}
Write-Host "TensorRT monitor dependencies: OK"

Write-Host "2. Checking the TensorRT engine..."
ssh $Jetson "test -s /home/jetson/models/dofbot_battery_yolov8n_512_fp16.engine"
if ($LASTEXITCODE -ne 0) {
    throw "TensorRT engine was not found on the Jetson."
}

Write-Host "3. Uploading the monitor-only inference service..."
scp $ScriptPath "${Jetson}:/home/jetson/dofbot_trt_inference.py"
if ($LASTEXITCODE -ne 0) { throw "Upload inference script failed." }
scp $UnitPath "${Jetson}:/home/jetson/$ServiceName"
if ($LASTEXITCODE -ne 0) { throw "Upload systemd unit failed." }

Write-Host "4. Installing and starting systemd service..."
ssh -t $Jetson "sudo install -m 0644 /home/jetson/$ServiceName /etc/systemd/system/$ServiceName && sudo systemctl daemon-reload && sudo systemctl enable --now $ServiceName && sudo systemctl --no-pager --full status $ServiceName"
if ($LASTEXITCODE -ne 0) { throw "Install/start TensorRT service failed." }

Write-Host "5. Checking HTTP health..."
$JetsonAddress = ($Jetson -split "@")[-1]
$HealthUri = "http://${JetsonAddress}:7101/health"
$HealthResponse = $null
$Deadline = [DateTime]::UtcNow.AddSeconds(
    [Math]::Max(10, $WaitSeconds)
)
do {
    try {
        $HealthResponse = Invoke-RestMethod `
            -Uri $HealthUri `
            -TimeoutSec 5
        break
    }
    catch {
        $LastHealthError = $_.Exception.Message
        Start-Sleep -Seconds 1
    }
}
while ([DateTime]::UtcNow -lt $Deadline)
if ($null -eq $HealthResponse) {
    throw (
        "TensorRT health check failed at ${HealthUri}: " +
        $LastHealthError
    )
}
if (-not $HealthResponse.success -or -not $HealthResponse.monitor_only) {
    throw "TensorRT service did not report healthy monitor-only mode."
}
if (
    ($HealthResponse.PSObject.Properties.Name -contains `
        "routes_robot_motion") -and
    $HealthResponse.routes_robot_motion
) {
    throw "Refusing a TensorRT service that reports robot motion routing."
}
$HealthResponse | ConvertTo-Json -Depth 6

Write-Host "TensorRT monitor-only inference service installed successfully."
