param(
    [string]$PythonCommand = "py",
    [string]$PythonVersion = "3.11"
)

$ErrorActionPreference = "Stop"
$MiniPcRoot = Split-Path -Parent $PSScriptRoot
$VenvPath = Join-Path $MiniPcRoot ".venv-minipc"
$PythonPath = Join-Path $VenvPath "Scripts\python.exe"
$ModelsPath = Join-Path $MiniPcRoot "models"

Write-Host "Battery AI Service - Mini PC CPU environment"
Write-Host "Target: Windows x64, CPU inference only"
Write-Host "Root  : $MiniPcRoot"

if (-not [Environment]::Is64BitOperatingSystem) {
    throw "The current Windows installation is not 64-bit. The WPF and PyTorch packages require x64 Windows."
}

try {
    & $PythonCommand "-$PythonVersion" -c "import struct,sys; assert struct.calcsize('P') * 8 == 64; print(sys.version)"
}
catch {
    throw "Python $PythonVersion x64 was not found. Install Python 3.11 x64, enable the Python launcher, then run this script again."
}

if (-not (Test-Path -LiteralPath $PythonPath)) {
    Write-Host "1. Creating virtual environment..."
    & $PythonCommand "-$PythonVersion" -m venv $VenvPath
}
else {
    Write-Host "1. Reusing existing environment: $VenvPath"
}

New-Item -ItemType Directory -Force -Path $ModelsPath | Out-Null

Write-Host "2. Updating pip tooling..."
& $PythonPath -m pip install --upgrade "pip<26" setuptools wheel

Write-Host "3. Installing CPU-only PyTorch..."
& $PythonPath -m pip install `
    --index-url https://download.pytorch.org/whl/cpu `
    torch==2.2.2 torchvision==0.17.2

Write-Host "4. Installing the pinned YOLO runtime..."
& $PythonPath -m pip install `
    numpy==1.26.4 `
    opencv-python==4.10.0.84 `
    ultralytics==8.4.115

Write-Host "5. Verifying imports..."
& $PythonPath -c "import cv2,numpy,torch,ultralytics; print('PyTorch:',torch.__version__); print('CUDA:',torch.cuda.is_available()); print('Ultralytics:',ultralytics.__version__); print('OpenCV:',cv2.__version__); print('NumPy:',numpy.__version__)"

Write-Host ""
Write-Host "Environment setup completed."
Write-Host "Next: copy best.pt into $ModelsPath\best.pt"
Write-Host "Then run: .\Start-MiniPC-AI-Service.ps1"
