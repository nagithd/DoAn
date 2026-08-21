[CmdletBinding()]
param(
    [string]$Jetson = "jetson@192.168.137.179"
)

$ErrorActionPreference = "Stop"
$ServiceName = "dofbot-trt-inference.service"

Write-Host "Disabling the optional TensorRT vision sidecar..."
ssh -t $Jetson "if systemctl list-unit-files $ServiceName >/dev/null 2>&1; then sudo systemctl disable --now $ServiceName; else echo '$ServiceName is not installed'; fi"
if ($LASTEXITCODE -ne 0) {
    throw "Cannot disable $ServiceName. Exit code: $LASTEXITCODE"
}

Write-Host "TensorRT vision sidecar is disabled."
