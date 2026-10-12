@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" RNIP
exit /b %ERRORLEVEL%
