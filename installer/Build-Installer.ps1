param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'OCIMS\eSureHi.csproj'
$releaseRoot = Join-Path $repoRoot 'release'
$publishDir = Join-Path $releaseRoot 'eSureHi-publish'
$innoScript = Join-Path $PSScriptRoot 'eSureHi.iss'
$innoCompiler = @(
    (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe')
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if (-not $innoCompiler) {
    throw 'Inno Setup 7 (or Inno Setup 6) is required to build the installer.'
}

Write-Host "Publishing eSureHi ($Configuration, $Runtime)..."
dotnet publish $projectPath -c $Configuration -r $Runtime --self-contained true -o $publishDir
if ($LASTEXITCODE -ne 0) {
    throw 'Application publish failed.'
}

Write-Host 'Building Inno Setup installer...'
& $innoCompiler "/DMyAppVersion=$Version" $innoScript
if ($LASTEXITCODE -ne 0) {
    throw 'Inno Setup compilation failed.'
}

# Remove obsolete artifacts left by the former SFX/IExpress installer.
$obsoleteArtifacts = @(
    'setup.exe', 'payload.zip', 'Install-eSureHi.ps1', 'RunInstaller.cmd',
    'README-Sulop-Setup.txt', 'eSureHi_Setup_Sulop.sed', 'RCX83CE.tmp',
    '~setup.CAB', '~setup.DDF', '~setup.RPT', '~setup_LAYOUT.INF',
    'installer-build.log', 'installer-build-error.log'
)
foreach ($artifact in $obsoleteArtifacts) {
    Remove-Item -LiteralPath (Join-Path $releaseRoot $artifact) -Force -ErrorAction SilentlyContinue
}
Remove-Item -LiteralPath (Join-Path $releaseRoot 'eSureHi-installer-staging') -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $publishDir -Recurse -Force -ErrorAction SilentlyContinue

$setupPath = Join-Path $releaseRoot 'eSureHi-Setup.exe'
Write-Host "Inno Setup installer created: $setupPath"
