@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" RND
exit /b %ERRORLEVEL%
