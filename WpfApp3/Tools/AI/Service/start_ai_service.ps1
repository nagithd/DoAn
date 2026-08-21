param(
    [string]$ModelPath = (Join-Path $PSScriptRoot "models\best.pt"),
    [int]$Port = 7100,
    [double]$Confidence = 0.25,
    [int]$ImageSize = 640,
    [string]$Device = "cpu"
)

$ErrorActionPreference = "Stop"
$ServiceScript = Join-Path $PSScriptRoot "ai_service.py"
$ConfiguredPython = $env:AI_PYTHON_PATH
$PythonCandidates = @(
    $ConfiguredPython,
    (Join-Path $PSScriptRoot ".venv\Scripts\python.exe"),
    (Join-Path $PSScriptRoot ".venv-minipc\Scripts\python.exe")
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
$PythonPath = $PythonCandidates |
    Where-Object { Test-Path -LiteralPath $_ } |
    Select-Object -First 1

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    throw (
        "Python AI environment was not found. Expected Service\.venv, " +
        "Service\.venv-minipc, or AI_PYTHON_PATH. Run " +
        "MiniPC\setup_minipc_yolo.ps1 to create one."
    )
}

if (-not (Test-Path -LiteralPath $ModelPath)) {
    throw "YOLO model not found: $ModelPath"
}

Write-Host "Starting Battery AI Service"
Write-Host "Python: $PythonPath"
Write-Host "Model : $ModelPath"
Write-Host "URL   : http://127.0.0.1:$Port"
Write-Host "Stop  : press Ctrl+C"

& $PythonPath $ServiceScript `
    --model $ModelPath `
    --host 127.0.0.1 `
    --port $Port `
    --conf $Confidence `
    --imgsz $ImageSize `
    --device $Device
