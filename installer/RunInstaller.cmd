@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-eSureHi.ps1" -LaunchAfterInstall
endlocal
