@echo off
setlocal EnableExtensions

:: Se non siamo già in modalità hidden, rilancia invisibile
if "%1" neq "hidden" (
  powershell -NoProfile -WindowStyle Hidden -Command ^
    "Start-Process -FilePath '%~f0' -ArgumentList 'hidden' -WindowStyle Hidden"
  exit /b
)

cd /d "%~dp0"

:: =========================
:: 0) Libera la porta 8085
:: =========================
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":8085" ^| findstr "LISTENING"') do (
  taskkill /PID %%p /F >nul 2>&1
)

:: =========================
:: 1) Avvia server e cattura PID
:: =========================
for /f "usebackq delims=" %%i in (`
  powershell -NoProfile -Command ^
    "$p=Start-Process -FilePath (Join-Path $PWD 'SQ_V1.exe') -WindowStyle Hidden -PassThru; $p.Id"
`) do set "SERVER_PID=%%i"

timeout /t 2 /nobreak >nul

set "URL=http://127.0.0.1:8085/?v=%RANDOM%%RANDOM%"
set "APP_PROFILE=%~dp0_app_profile"

if not exist "%APP_PROFILE%" mkdir "%APP_PROFILE%" >nul 2>&1

set "CHROME=%ProgramFiles%\Google\Chrome\Application\chrome.exe"
if not exist "%CHROME%" set "CHROME=%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe"

set "EDGE=%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe"
if not exist "%EDGE%" set "EDGE=%ProgramFiles%\Microsoft\Edge\Application\msedge.exe"

if exist "%CHROME%" (
  powershell -NoProfile -Command ^
    "$exe='%CHROME%'; $args=@('--user-data-dir=%APP_PROFILE%','--no-first-run','--no-default-browser-check','--app=%URL%');" ^
    "$p=Start-Process -FilePath $exe -ArgumentList $args -PassThru; $p.WaitForExit()"
  goto :CLOSE_SERVER
)

if exist "%EDGE%" (
  powershell -NoProfile -Command ^
    "$exe='%EDGE%'; $args=@('--user-data-dir=%APP_PROFILE%','--no-first-run','--no-default-browser-check','--app=%URL%');" ^
    "$p=Start-Process -FilePath $exe -ArgumentList $args -PassThru; $p.WaitForExit()"
  goto :CLOSE_SERVER
)

start "" "%URL%"
goto :eof

:CLOSE_SERVER
taskkill /PID %SERVER_PID% /F >nul 2>&1
endlocal
