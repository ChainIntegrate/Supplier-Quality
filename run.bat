@echo off
setlocal EnableExtensions EnableDelayedExpansion

:: Rilancio invisibile
if /I "%~1" neq "hidden" (
  powershell -NoProfile -WindowStyle Hidden -Command ^
    "Start-Process -FilePath '%~f0' -ArgumentList 'hidden' -WindowStyle Hidden"
  exit /b
)

cd /d "%~dp0"

:: Check exe
if not exist "%~dp0SQ_V1.exe" (
  powershell -NoProfile -Command "[System.Windows.MessageBox]::Show('SQ_V1.exe non trovato nella cartella','Run','OK','Error')"
  exit /b
)

:: =========================
:: 0) Libera la porta 8085 (solo LISTENING)
:: =========================
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":8085" ^| findstr "LISTENING"') do (
  taskkill /PID %%p /F >nul 2>&1
)

:: =========================
:: 1) Avvia server hidden e cattura PID
:: =========================
set "SERVER_PID_FILE=%~dp0._server_pid.txt"
del /f /q "%SERVER_PID_FILE%" >nul 2>&1

powershell -NoProfile -WindowStyle Hidden -Command ^
  "$p=Start-Process -FilePath (Join-Path $pwd 'SQ_V1.exe') -WindowStyle Hidden -PassThru; " ^
  "Set-Content -LiteralPath '%SERVER_PID_FILE%' -Value $p.Id -Encoding ASCII" >nul 2>&1

if not exist "%SERVER_PID_FILE%" (
  powershell -NoProfile -Command "[System.Windows.MessageBox]::Show('Avvio server fallito (PID non ottenuto).','Run','OK','Error')"
  exit /b
)

set /p SERVER_PID=<"%SERVER_PID_FILE%"
if not defined SERVER_PID (
  powershell -NoProfile -Command "[System.Windows.MessageBox]::Show('Avvio server fallito (PID vuoto).','Run','OK','Error')"
  exit /b
)

:: =========================
:: 2) Aspetta che la porta sia su (max 20s)
:: =========================
set "READY=0"
for /l %%t in (1,1,20) do (
  for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":8085" ^| findstr "LISTENING"') do set "READY=1"
  if "!READY!"=="1" goto :PORT_OK
  timeout /t 1 /nobreak >nul
)

:: Se non è su, chiudi server e segnala
taskkill /PID !SERVER_PID! /F >nul 2>&1
powershell -NoProfile -Command "[System.Windows.MessageBox]::Show('Server non in ascolto su 127.0.0.1:8085 (entro 20s).','Run','OK','Error')"
exit /b

:PORT_OK

:: =========================
:: 3) Apri browser in app-mode e aspetta chiusura
:: =========================
set "URL=http://127.0.0.1:8085/?v=%RANDOM%%RANDOM%"
set "APP_PROFILE=%~dp0_app_profile"
if not exist "%APP_PROFILE%" mkdir "%APP_PROFILE%" >nul 2>&1

set "CHROME=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not exist "%CHROME%" set "CHROME=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"

set "EDGE=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
if not exist "%EDGE%" set "EDGE=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"

set "BROWSER_EXE="
if exist "%CHROME%" set "BROWSER_EXE=%CHROME%"
if not defined BROWSER_EXE if exist "%EDGE%" set "BROWSER_EXE=%EDGE%"

if not defined BROWSER_EXE (
  :: Senza browser app-mode non posso “aspettare” e quindi non posso chiudere bene il server
  taskkill /PID !SERVER_PID! /F >nul 2>&1
  powershell -NoProfile -Command "[System.Windows.MessageBox]::Show('Chrome/Edge non trovati. Impossibile aprire in app-mode e chiudere il server automaticamente.','Run','OK','Error')"
  exit /b
)

set "BROWSER_PID_FILE=%~dp0._browser_pid.txt"
del /f /q "%BROWSER_PID_FILE%" >nul 2>&1

powershell -NoProfile -WindowStyle Hidden -Command ^
  "$exe='%BROWSER_EXE%'; " ^
  "$args=@('--user-data-dir=%APP_PROFILE%','--no-first-run','--no-default-browser-check','--app=%URL%'); " ^
  "$b=Start-Process -FilePath $exe -ArgumentList $args -PassThru; " ^
  "Set-Content -LiteralPath '%BROWSER_PID_FILE%' -Value $b.Id -Encoding ASCII; " ^
  "Wait-Process -Id $b.Id" >nul 2>&1

:: =========================
:: 4) Browser chiuso -> chiudi server
:: =========================
taskkill /PID !SERVER_PID! /F >nul 2>&1

:: pulizia
del /f /q "%SERVER_PID_FILE%" "%BROWSER_PID_FILE%" >nul 2>&1
endlocal
