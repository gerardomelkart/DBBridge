@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" RMJ_D
exit /b %ERRORLEVEL%
