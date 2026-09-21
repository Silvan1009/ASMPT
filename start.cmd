@echo off
rem Double-click entry point for start.ps1. -ExecutionPolicy Bypass applies to this one PowerShell process
rem only; a fresh Windows installation refuses to run scripts otherwise.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0start.ps1" %*
set EXITCODE=%ERRORLEVEL%
pause
exit /b %EXITCODE%
