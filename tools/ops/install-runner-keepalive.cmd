@echo off
REM ============================================================================
REM  Far Horizon - install the runner keep-alive + re-enable the watchdog.
REM
REM  JUST DOUBLE-CLICK THIS FILE. It self-elevates; click Yes on the UAC prompt.
REM
REM  Every Task Scheduler write on this machine is denied to a non-elevated
REM  user (verified 2026-08-02), which is why elevation is unavoidable here.
REM ============================================================================

setlocal
set "PS1=%~dp0install-runner-keepalive.ps1"

REM Already elevated? Then just run it.
net session >nul 2>&1
if %errorlevel% equ 0 (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
    echo.
    pause
    exit /b
)

REM Not elevated - relaunch this same .cmd through UAC.
echo Requesting administrator rights...
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command ^
  "Start-Process -FilePath '%~f0' -Verb RunAs"
exit /b
