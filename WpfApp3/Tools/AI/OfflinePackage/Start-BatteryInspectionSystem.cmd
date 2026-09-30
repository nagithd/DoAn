@echo off
setlocal

set "ROOT=%~dp0"
set "AI_EXE=%ROOT%AIService\BatteryAIService.exe"
set "MODEL=%ROOT%AIService\models\best.pt"
set "WPF_EXE=%ROOT%WpfApp3.exe"

if not exist "%AI_EXE%" (
    echo [ERROR] AI Service was not found: "%AI_EXE%"
    pause
    exit /b 1
)

if not exist "%MODEL%" (
    echo [ERROR] YOLO model was not found: "%MODEL%"
    pause
    exit /b 1
)

if not exist "%WPF_EXE%" (
    echo [ERROR] WPF application was not found: "%WPF_EXE%"
    pause
    exit /b 1
)

tasklist /FI "IMAGENAME eq BatteryAIService.exe" 2>NUL | find /I "BatteryAIService.exe" >NUL
if errorlevel 1 (
    echo Starting Battery AI Service...
    start "Battery AI Service" /min "%AI_EXE%" --model "%MODEL%" --host 127.0.0.1 --port 7100 --device cpu --imgsz 512
) else (
    echo Battery AI Service is already running.
)

echo Waiting for the AI Service...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "$deadline=(Get-Date).AddSeconds(45); do { try { $r=Invoke-RestMethod -Uri 'http://127.0.0.1:7100/health' -TimeoutSec 2; if ($r.status -eq 'ok') { exit 0 } } catch {}; Start-Sleep -Milliseconds 500 } while ((Get-Date) -lt $deadline); exit 1"

if errorlevel 1 (
    echo [WARNING] AI Service did not become ready within 45 seconds.
    echo The WPF interface will still open. Check its System Log for details.
) else (
    echo AI Service is ready.
)

start "Battery Inspection System" "%WPF_EXE%"
endlocal
