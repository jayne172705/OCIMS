$exePath = "OCIMS/bin/x64/Debug/net9.0-windows/eSureHi.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "Warning: Build output not found at $exePath" -ForegroundColor Yellow
    Write-Host "Please run .\build.ps1 first to build the project." -ForegroundColor Yellow
    exit 1
}

Write-Host "Running application (Platform=x64)..." -ForegroundColor Cyan
dotnet run --project OCIMS/eSureHi.csproj --no-build --property:Platform=x64
