$ErrorActionPreference = "Stop"
dotnet build .\RemoteDock.sln -c Release
Write-Host "Build complete: .\RemoteDock\bin\Release\net8.0-windows\RemoteDock.exe"
