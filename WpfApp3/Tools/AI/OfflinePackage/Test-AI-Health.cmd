@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "try { Invoke-RestMethod -Uri 'http://127.0.0.1:7100/health' -TimeoutSec 10 | Format-List } catch { Write-Host ('AI Service is unavailable: ' + $_.Exception.Message) -ForegroundColor Red; exit 1 }"
pause

