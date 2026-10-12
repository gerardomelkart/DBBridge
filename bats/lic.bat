@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" LIC
exit /b %ERRORLEVEL%
