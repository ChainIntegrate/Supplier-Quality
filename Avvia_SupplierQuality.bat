@echo off
setlocal
cd /d "%~dp0"

:: Avvia l'applicazione (server locale)
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -Command ^
  "Start-Process -FilePath '.\SupplierQuality.exe' -WindowStyle Hidden"

:: Attendi che il server sia pronto
timeout /t 2 /nobreak >nul

:: Prova Chrome in modalità app
if exist "%ProgramFiles%\Google\Chrome\Application\chrome.exe" (
  "%ProgramFiles%\Google\Chrome\Application\chrome.exe" --app=http://127.0.0.1:8085/
  goto :eof
)

if exist "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" (
  "%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe" --app=http://127.0.0.1:8085/
  goto :eof
)

:: Fallback: browser predefinito
start "" "http://127.0.0.1:8085/"

endlocal
