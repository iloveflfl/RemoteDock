@echo off
setlocal
cd /d "%~dp0"
dotnet restore RemoteDock.sln
if errorlevel 1 exit /b %errorlevel%
dotnet build RemoteDock.sln -c Release
if errorlevel 1 exit /b %errorlevel%
echo.
echo Build complete.
echo Run: RemoteDock\bin\Release\net8.0-windows\RemoteDock.exe
