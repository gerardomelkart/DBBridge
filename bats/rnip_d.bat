@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" RNIP_D
exit /b %ERRORLEVEL%
