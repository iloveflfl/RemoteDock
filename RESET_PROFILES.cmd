@echo off
setlocal
set APPDIR=%APPDATA%\RemoteDock
if exist "%APPDIR%\profiles.json" (
  copy "%APPDIR%\profiles.json" "%APPDIR%\profiles.backup.json" >nul
  del "%APPDIR%\profiles.json"
  echo Deleted profiles.json. Backup saved as profiles.backup.json
) else (
  echo No profiles.json found.
)
pause
