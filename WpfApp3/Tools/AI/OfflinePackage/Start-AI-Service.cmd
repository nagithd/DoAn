@echo off
setlocal
set "ROOT=%~dp0"

"%ROOT%AIService\BatteryAIService.exe" ^
  --model "%ROOT%AIService\models\best.pt" ^
  --host 127.0.0.1 ^
  --port 7100 ^
  --device cpu ^
  --imgsz 512

if errorlevel 1 pause
endlocal
