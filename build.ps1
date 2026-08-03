$processes = Get-Process -Name "eSureHi" -ErrorAction SilentlyContinue
if ($processes) {
    Write-Host "Stopping running eSureHi processes to prevent file locks..." -ForegroundColor Yellow
    $processes | Stop-Process -Force
    Start-Sleep -Seconds 1
}

Write-Host "Building eSureHi solution..." -ForegroundColor Cyan
dotnet build eSureHi.sln
if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Succeeded!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nBuild Failed!" -ForegroundColor Red
    exit 1
}
