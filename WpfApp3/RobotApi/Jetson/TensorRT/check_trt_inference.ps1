[CmdletBinding()]
param(
    [string]$Jetson = "jetson@192.168.137.179",
    [string]$JetsonIp = "192.168.137.179"
)

$ErrorActionPreference = "Stop"
$ServiceName = "dofbot-trt-inference.service"

Write-Host "===== TensorRT service ====="
ssh -t $Jetson "sudo systemctl --no-pager --full status $ServiceName"

Write-Host "`n===== TensorRT journal ====="
ssh -t $Jetson "sudo journalctl -b -u $ServiceName -n 80 --no-pager"

Write-Host "`n===== TensorRT health ====="
try {
    $Health = Invoke-RestMethod "http://${JetsonIp}:7101/health"
    $Health | ConvertTo-Json -Depth 6
    if (-not $Health.monitor_only) {
        Write-Warning "TensorRT service is not marked monitor-only."
    }
    if (
        ($Health.PSObject.Properties.Name -contains `
            "routes_robot_motion") -and
        $Health.routes_robot_motion
    ) {
        Write-Warning "TensorRT service reports robot motion routing."
    }
}
catch {
    Write-Warning $_.Exception.Message
}
