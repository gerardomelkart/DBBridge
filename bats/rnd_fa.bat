@echo off
cd /d "%~dp0"
"%~dp0DBBridge.exe" RND_FA
exit /b %ERRORLEVEL%
