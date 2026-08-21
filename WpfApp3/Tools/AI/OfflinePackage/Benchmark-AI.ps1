param(
    [Parameter(Mandatory = $true)]
    [string]$ImagePath,

    [string]$ModelPath = "",

    [ValidateRange(1, 200)]
    [int]$Iterations = 20,

    [ValidateRange(0, 20)]
    [int]$WarmupIterations = 3,

    [ValidateRange(1, 65535)]
    [int]$Port = 7100,

    [ValidateRange(32, 4096)]
    [int]$ImageSize = 512,

    [ValidateRange(1, 32)]
    [int]$CpuThreads = 2,

    [switch]$KeepServiceRunning
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$AiExecutable = Join-Path $Root "AIService\BatteryAIService.exe"
$DefaultModelPath = Join-Path $Root "AIService\models\best.pt"
if ([string]::IsNullOrWhiteSpace($ModelPath)) {
    $ModelPath = $DefaultModelPath
}
$ModelPath = (Resolve-Path -LiteralPath $ModelPath).Path
$ResolvedImage = (Resolve-Path -LiteralPath $ImagePath).Path
$BaseUrl = "http://127.0.0.1:$Port"
$StartedByBenchmark = $false
$AiProcess = $null

$ThreadCountText = [Math]::Max(1, $CpuThreads).ToString()
$env:OMP_NUM_THREADS = $ThreadCountText
$env:MKL_NUM_THREADS = $ThreadCountText
$env:OPENBLAS_NUM_THREADS = $ThreadCountText
$env:NUMEXPR_NUM_THREADS = $ThreadCountText

function Get-Statistics {
    param([double[]]$Values)

    $Sorted = @($Values | Sort-Object)
    $Count = $Sorted.Count
    $Average = ($Sorted | Measure-Object -Average).Average
    $P50Index = [Math]::Min($Count - 1, [Math]::Floor(($Count - 1) * 0.50))
    $P95Index = [Math]::Min($Count - 1, [Math]::Ceiling(($Count - 1) * 0.95))

    [PSCustomObject]@{
        Average = [Math]::Round($Average, 1)
        Minimum = [Math]::Round($Sorted[0], 1)
        P50 = [Math]::Round($Sorted[$P50Index], 1)
        P95 = [Math]::Round($Sorted[$P95Index], 1)
        Maximum = [Math]::Round($Sorted[-1], 1)
    }
}

function Test-AiHealth {
    try {
        $Health = Invoke-RestMethod -Uri "$BaseUrl/health" -TimeoutSec 3
        return $Health.status -eq "ok"
    }
    catch {
        return $false
    }
}

function Invoke-Prediction {
    param([string]$InspectionId)

    $Body = @{
        image_path = $ResolvedImage
        inspection_id = $InspectionId
    } | ConvertTo-Json -Compress

    $Timer = [Diagnostics.Stopwatch]::StartNew()
    $Response = Invoke-RestMethod `
        -Method Post `
        -Uri "$BaseUrl/predict" `
        -ContentType "application/json" `
        -Body $Body `
        -TimeoutSec 120
    $Timer.Stop()

    [PSCustomObject]@{
        RoundTripMs = $Timer.Elapsed.TotalMilliseconds
        InferenceMs = [double]$Response.inference_time_ms
        Class = [string]$Response.detected_class
        Confidence = [double]$Response.confidence
        Detections = @($Response.detections).Count
    }
}

if (-not (Test-Path -LiteralPath $AiExecutable)) {
    throw "BatteryAIService.exe was not found: $AiExecutable"
}
if (-not (Test-Path -LiteralPath $ModelPath)) {
    throw "Model was not found: $ModelPath"
}

try {
    $ColdStartTimer = [Diagnostics.Stopwatch]::StartNew()
    if (-not (Test-AiHealth)) {
        Write-Host "Starting the packaged AI Service..."
        $AiProcess = Start-Process `
            -FilePath $AiExecutable `
            -ArgumentList @(
                "--model", "`"$ModelPath`"",
                "--host", "127.0.0.1",
                "--port", "$Port",
                "--device", "cpu",
                "--imgsz", "$ImageSize"
            ) `
            -WindowStyle Hidden `
            -PassThru
        $StartedByBenchmark = $true

        $Deadline = (Get-Date).AddSeconds(120)
        while (-not (Test-AiHealth) -and (Get-Date) -lt $Deadline) {
            Start-Sleep -Milliseconds 500
        }
        if (-not (Test-AiHealth)) {
            throw "AI Service did not become healthy within 120 seconds."
        }
    }
    $ColdStartTimer.Stop()

    if ($null -eq $AiProcess) {
        $AiProcess = Get-Process -Name "BatteryAIService" -ErrorAction SilentlyContinue |
            Select-Object -First 1
    }

    Write-Host "Warm-up: $WarmupIterations iteration(s)..."
    for ($Index = 1; $Index -le $WarmupIterations; $Index++) {
        $null = Invoke-Prediction -InspectionId "warmup-$Index"
    }

    if ($null -ne $AiProcess) {
        $AiProcess.Refresh()
        $CpuStart = $AiProcess.TotalProcessorTime.TotalSeconds
    }
    else {
        $CpuStart = 0.0
    }

    $TestTimer = [Diagnostics.Stopwatch]::StartNew()
    $Rows = @()
    for ($Index = 1; $Index -le $Iterations; $Index++) {
        $Result = Invoke-Prediction -InspectionId "benchmark-$Index"
        $Rows += [PSCustomObject]@{
            Iteration = $Index
            InferenceMs = [Math]::Round($Result.InferenceMs, 1)
            RoundTripMs = [Math]::Round($Result.RoundTripMs, 1)
            Class = $Result.Class
            Confidence = [Math]::Round($Result.Confidence, 4)
            Detections = $Result.Detections
        }
        Write-Host ("{0,3}/{1}: inference={2,7:N1} ms, total={3,7:N1} ms, class={4}" -f `
            $Index, $Iterations, $Result.InferenceMs, $Result.RoundTripMs, $Result.Class)
    }
    $TestTimer.Stop()

    $InferenceStats = Get-Statistics -Values ([double[]]$Rows.InferenceMs)
    $RoundTripStats = Get-Statistics -Values ([double[]]$Rows.RoundTripMs)

    $CpuPercent = $null
    $WorkingSetMiB = $null
    $PeakWorkingSetMiB = $null
    if ($null -ne $AiProcess -and -not $AiProcess.HasExited) {
        $AiProcess.Refresh()
        $CpuSeconds = $AiProcess.TotalProcessorTime.TotalSeconds - $CpuStart
        $CpuPercent = [Math]::Round(
            100.0 * $CpuSeconds /
            [Math]::Max(0.001, $TestTimer.Elapsed.TotalSeconds) /
            [Environment]::ProcessorCount,
            1)
        $WorkingSetMiB = [Math]::Round($AiProcess.WorkingSet64 / 1MB, 1)
        $PeakWorkingSetMiB = [Math]::Round($AiProcess.PeakWorkingSet64 / 1MB, 1)
    }

    $CsvPath = Join-Path $Root (
        "AI_Benchmark_{0}.csv" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
    $Rows | Export-Csv -LiteralPath $CsvPath -NoTypeInformation -Encoding UTF8

    Write-Host ""
    Write-Host "===== MINI PC AI BENCHMARK ====="
    Write-Host "Model                      : $ModelPath"
    Write-Host "Image size / CPU threads   : $ImageSize / $CpuThreads"
    if ($StartedByBenchmark) {
        Write-Host ("Cold model/service startup : {0:N1} ms" -f $ColdStartTimer.Elapsed.TotalMilliseconds)
    }
    else {
        Write-Host "Cold model/service startup : not measured (service was already running)"
    }
    Write-Host ("AI inference average       : {0:N1} ms" -f $InferenceStats.Average)
    Write-Host ("AI inference P50 / P95     : {0:N1} / {1:N1} ms" -f $InferenceStats.P50, $InferenceStats.P95)
    Write-Host ("AI inference min / max     : {0:N1} / {1:N1} ms" -f $InferenceStats.Minimum, $InferenceStats.Maximum)
    Write-Host ("HTTP total average         : {0:N1} ms" -f $RoundTripStats.Average)
    Write-Host ("HTTP total P50 / P95       : {0:N1} / {1:N1} ms" -f $RoundTripStats.P50, $RoundTripStats.P95)
    if ($null -ne $CpuPercent) {
        Write-Host ("AI process average CPU     : {0:N1}% of total machine" -f $CpuPercent)
        Write-Host ("Working set / peak RAM     : {0:N1} / {1:N1} MiB" -f $WorkingSetMiB, $PeakWorkingSetMiB)
    }
    Write-Host "Detailed results           : $CsvPath"
}
finally {
    if ($StartedByBenchmark -and -not $KeepServiceRunning -and
        $null -ne $AiProcess -and -not $AiProcess.HasExited) {
        Stop-Process -Id $AiProcess.Id -Force
    }
}
