$ErrorActionPreference = "Stop"
$ServiceRoot = Split-Path -Parent $PSScriptRoot
$PythonPath = Join-Path $ServiceRoot ".venv-minipc\Scripts\python.exe"
$ModelPath = Join-Path $ServiceRoot "models\best.pt"

if (-not (Test-Path -LiteralPath $PythonPath)) {
    throw "Mini PC Python environment not found. Run setup_minipc_yolo.ps1 first."
}

Write-Host "===== Python environment ====="
& $PythonPath -c "import platform,cv2,numpy,torch,ultralytics; print('Python:',platform.python_version()); print('Architecture:',platform.architecture()[0]); print('PyTorch:',torch.__version__); print('CUDA:',torch.cuda.is_available()); print('Ultralytics:',ultralytics.__version__); print('OpenCV:',cv2.__version__); print('NumPy:',numpy.__version__); print('Torch threads:',torch.get_num_threads())"

if (Test-Path -LiteralPath $ModelPath) {
    Write-Host ""
    Write-Host "===== Model ====="
    & $PythonPath -c "from ultralytics import YOLO; m=YOLO(r'$ModelPath'); print('Task:',m.task); print('Classes:',m.names)"
}
else {
    Write-Warning "Model is not present yet: $ModelPath"
}

Write-Host ""
Write-Host "===== Local service ====="
try {
    Invoke-RestMethod "http://127.0.0.1:7100/health" |
        ConvertTo-Json -Depth 10
}
catch {
    Write-Warning "AI Service is not running on port 7100. Start it in another PowerShell window."
}
