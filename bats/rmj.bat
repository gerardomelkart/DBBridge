@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" RMJ
exit /b %ERRORLEVEL%
