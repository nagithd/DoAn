param(
    [string]$ModelPath = "D:\capstone\AIService\runs\detect\finetune_battery_v2_20260804_232921\weights\best.pt",
    [int]$Port = 7100,
    [double]$Confidence = 0.25,
    [int]$ImageSize = 768,
    [string]$Device = "cpu"
)

$ErrorActionPreference = "Stop"
$PythonPath = "D:\capstone\AIService\venv\Scripts\python.exe"
$ServiceScript = Join-Path $PSScriptRoot "ai_service.py"

if (-not (Test-Path -LiteralPath $PythonPath)) {
    throw "Python environment not found: $PythonPath"
}
if (-not (Test-Path -LiteralPath $ModelPath)) {
    throw "YOLO model not found: $ModelPath"
}

Write-Host "Starting Battery AI Service"
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
