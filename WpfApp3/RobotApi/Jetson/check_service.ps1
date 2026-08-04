param(
    [string]$Jetson = "jetson@192.168.137.179",
    [string]$JetsonIp = "192.168.137.179",
    [int]$WaitSeconds = 300
)

$ErrorActionPreference = "Continue"
$ServiceName = "dofbot-robot-api.service"
$HealthUri = "http://${JetsonIp}:7000/health"
$health = $null
$lastHealthError = $null

Write-Host "===== waiting for HTTP health ====="
$deadline = [DateTime]::UtcNow.AddSeconds(
    [Math]::Max(0, $WaitSeconds)
)

do {
    try {
        $health = Invoke-RestMethod `
            -Uri $HealthUri `
            -Method Get `
            -TimeoutSec 4
        break
    }
    catch {
        $lastHealthError = $_.Exception.Message
    }

    if ([DateTime]::UtcNow -ge $deadline) {
        break
    }

    Write-Host "Robot API is not ready yet; retrying in 5 seconds..."
    Start-Sleep -Seconds 5
}
while ([DateTime]::UtcNow -lt $deadline)

Write-Host ""
Write-Host "===== systemd status and latest journal ====="
ssh -t $Jetson @"
sudo systemctl --no-pager --full status '$ServiceName'
echo
echo '===== latest journal ====='
sudo journalctl -b -u '$ServiceName' -n 80 --no-pager
"@

Write-Host ""
Write-Host "===== HTTP health ====="
if ($null -ne $health) {
    $health | ConvertTo-Json -Depth 6
}
else {
    Write-Warning (
        "Robot API did not become healthy within " +
        "$WaitSeconds seconds. Last error: $lastHealthError"
    )
}
