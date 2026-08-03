Write-Host "Building eSureHi solution..." -ForegroundColor Cyan
dotnet build eSureHi.sln
if ($LASTEXITCODE -eq 0) {
    Write-Host "`nBuild Succeeded!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "`nBuild Failed!" -ForegroundColor Red
    exit 1
}
