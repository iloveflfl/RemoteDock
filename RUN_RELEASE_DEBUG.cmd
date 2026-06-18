@echo off
setlocal
cd /d "%~dp0"
echo [RemoteDock] Working dir: %CD%
echo [RemoteDock] Checking dotnet...
dotnet --info
if errorlevel 1 (
  echo.
  echo dotnet command failed. Install .NET 8 Desktop Runtime or .NET 8 SDK.
  pause
  exit /b 1
)
echo.
echo [RemoteDock] Running Release DLL with dotnet host...
echo If the app closes or shows an error, check: %%APPDATA%%\RemoteDock\crash.log
dotnet "%~dp0RemoteDock\bin\Release\net8.0-windows\RemoteDock.dll"
echo.
echo [RemoteDock] Process returned. ExitCode=%ERRORLEVEL%
echo Crash log location: %APPDATA%\RemoteDock\crash.log
pause
