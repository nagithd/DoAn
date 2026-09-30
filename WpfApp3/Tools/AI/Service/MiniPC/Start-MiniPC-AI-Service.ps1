param(
    [string]$ModelPath = (Join-Path (Split-Path -Parent $PSScriptRoot) "models\best.pt"),
    [int]$Port = 7100,
    [double]$Confidence = 0.25,
    [int]$ImageSize = 512,
    [int]$CpuThreads = 2
)

$ErrorActionPreference = "Stop"
$ServiceRoot = Split-Path -Parent $PSScriptRoot
$PythonPath = Join-Path $ServiceRoot ".venv-minipc\Scripts\python.exe"
$ServiceScript = Join-Path $ServiceRoot "ai_service.py"

if (-not (Test-Path -LiteralPath $PythonPath)) {
    throw "Mini PC Python environment not found. Run MiniPC\setup_minipc_yolo.ps1 first."
}
if (-not (Test-Path -LiteralPath $ServiceScript)) {
    throw "AI service script not found: $ServiceScript"
}
if (-not (Test-Path -LiteralPath $ModelPath)) {
    throw "YOLO model not found: $ModelPath"
}

$threadCount = [Math]::Max(1, $CpuThreads).ToString()
$env:OMP_NUM_THREADS = $threadCount
$env:MKL_NUM_THREADS = $threadCount
$env:OPENBLAS_NUM_THREADS = $threadCount
$env:NUMEXPR_NUM_THREADS = $threadCount

Write-Host "Starting Battery AI Service for the Mini PC"
Write-Host "Model       : $ModelPath"
Write-Host "Image size  : $ImageSize"
Write-Host "CPU threads : $threadCount"
Write-Host "Health URL  : http://127.0.0.1:$Port/health"
Write-Host "Stop        : press Ctrl+C"

& $PythonPath $ServiceScript `
    --model $ModelPath `
    --host 127.0.0.1 `
    --port $Port `
    --conf $Confidence `
    --imgsz $ImageSize `
    --device cpu
