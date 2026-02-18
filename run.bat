@echo off
setlocal EnableExtensions EnableDelayedExpansion
cd /d "%~dp0"

set "PORT=8085"
set "URL=http://127.0.0.1:%PORT%/"
set "SERVER=SQ_V2.exe"

if not exist "%SERVER%" (
  echo ERRORE: %SERVER% non trovato.
  pause
  exit /b 1
)

:: libera la porta se occupata
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":%PORT%" ^| findstr "LISTENING"') do (
  taskkill /PID %%p /F >nul 2>&1
)

:: avvia server in background (completamente invisibile)
start "" /min "%SERVER%"

:: piccola attesa tecnica (1 secondo basta)
timeout /t 1 /nobreak >nul

:: apri Edge guest in modalità app (no sync)
start "" msedge ^
  --guest ^
  --no-first-run ^
  --no-default-browser-check ^
  --app="%URL%?v=%RANDOM%%RANDOM%"

exit /b 0
