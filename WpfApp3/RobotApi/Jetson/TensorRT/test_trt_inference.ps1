[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,
    [string]$JetsonIp = "192.168.137.179",
    [int]$Port = 7101,
    [int]$Iterations = 5
)

$ErrorActionPreference = "Stop"
if (-not (Test-Path -LiteralPath $ImagePath -PathType Leaf)) {
    throw "Test image was not found: $ImagePath"
}
if ($Iterations -lt 1) {
    throw "Iterations must be at least 1."
}

$BaseUri = "http://${JetsonIp}:$Port"
$Health = Invoke-RestMethod `
    -Uri "$BaseUri/health" `
    -TimeoutSec 10
if (-not $Health.success -or -not $Health.monitor_only) {
    throw "TensorRT sidecar is not healthy in monitor-only mode."
}
if (
    ($Health.PSObject.Properties.Name -contains "routes_robot_motion") -and
    $Health.routes_robot_motion
) {
    throw "Refusing to test an inference service that routes robot motion."
}

$Bytes = [System.IO.File]::ReadAllBytes(
    (Resolve-Path -LiteralPath $ImagePath).Path)
$Rows = @()
$LastResponse = $null
for ($Run = 1; $Run -le $Iterations; $Run++) {
    $Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $LastResponse = Invoke-RestMethod `
        -Uri "$BaseUri/infer" `
        -Method Post `
        -ContentType "image/jpeg" `
        -Body $Bytes `
        -TimeoutSec 30
    $Stopwatch.Stop()

    if (-not $LastResponse.success -or -not $LastResponse.monitor_only) {
        throw "Inference response did not preserve monitor-only mode."
    }
    $TopConfidence = $null
    if ($LastResponse.detections.Count -gt 0) {
        $TopConfidence = $LastResponse.detections[0].confidence
    }
    $Rows += [PSCustomObject]@{
        Run = $Run
        InferenceMs = $LastResponse.inference_ms
        HttpTotalMs = [Math]::Round(
            $Stopwatch.Elapsed.TotalMilliseconds,
            2)
        Detections = $LastResponse.detections.Count
        TopConfidence = $TopConfidence
    }
}

Write-Host "===== monitor-only inference benchmark ====="
$Rows | Format-Table -AutoSize
Write-Host ""
Write-Host "===== final response ====="
$LastResponse | ConvertTo-Json -Depth 10
Write-Host ""
Write-Host "No robot API or motion endpoint was called."
