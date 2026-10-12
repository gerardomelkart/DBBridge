@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" LIC_D
exit /b %ERRORLEVEL%
