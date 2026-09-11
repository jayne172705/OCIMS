param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64',
    [string]$Version = '1.0.0',
    [switch]$SkipIExpress
)

$ErrorActionPreference = 'Stop'

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$projectPath = Join-Path $repoRoot 'OCIMS\eSureHi.csproj'
$releaseRoot = Join-Path $repoRoot 'release'
$publishDir = Join-Path $releaseRoot 'eSureHi-publish'
$stageDir = Join-Path $releaseRoot 'eSureHi-installer-staging'
$payloadWorkDir = Join-Path $env:TEMP 'eSureHi-installer-payload-work'
$payloadAppDir = Join-Path $payloadWorkDir 'app'
$payloadZip = Join-Path $stageDir 'payload.zip'
$setupExe = Join-Path $releaseRoot 'setup.exe'
$sedPath = Join-Path $releaseRoot 'eSureHi_Setup_Sulop.sed'

Write-Host "Publishing eSureHi ($Configuration, $Runtime)..."
dotnet publish $projectPath -c $Configuration -r $Runtime --self-contained true -o $publishDir

# ── Inject prefilled DB configs in sync with app + installer dialog ──
# KEEP IN SYNC with Install-eSureHi.ps1 and
# OCIMS/ViewModels/Shared/ConnectionSettingsViewModel.cs (Network/Online presets).
@(
    'Server=192.168.0.47'
    'Port=3306'
    'Database=ims_db'
    'User=root'
    'Password=network@2026'
) | Set-Content -LiteralPath (Join-Path $publishDir 'eSureHiConfig.txt') -Encoding UTF8
@(
    'Server=192.168.0.47'
    'Port=3306'
    'Database=ggms_db'
    'User=root'
    'Password=network@2026'
) | Set-Content -LiteralPath (Join-Path $publishDir 'GgmsConfig.txt') -Encoding UTF8
@(
    'Server=192.168.0.47'
    'Port=3306'
    'Database=crs_db'
    'User=root'
    'Password=network@2026'
) | Set-Content -LiteralPath (Join-Path $publishDir 'CrsConfig.txt') -Encoding UTF8
Write-Host 'Prefilled Network configs injected into publish output.'

if (Test-Path -LiteralPath $stageDir) {
    Remove-Item -LiteralPath $stageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $stageDir -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install-eSureHi.ps1') -Destination $stageDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'RunInstaller.cmd') -Destination $stageDir -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README-Sulop-Setup.txt') -Destination $stageDir -Force

Remove-Item -LiteralPath $payloadWorkDir -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $payloadAppDir -Force | Out-Null
Copy-Item -Path (Join-Path $publishDir '*') -Destination $payloadAppDir -Recurse -Force
Compress-Archive -Path (Join-Path $payloadWorkDir 'app') -DestinationPath $payloadZip -Force

$sed = @"
[Version]
Class=IEXPRESS
SEDVersion=3

[Options]
PackagePurpose=InstallApp
ShowInstallProgramWindow=0
HideExtractAnimation=1
UseLongFileName=1
InsideCompressed=0
CAB_FixedSize=0
CAB_ResvCodeSigning=0
RebootMode=N
InstallPrompt=%InstallPrompt%
DisplayLicense=%DisplayLicense%
FinishMessage=%FinishMessage%
TargetName=$setupExe
FriendlyName=eSureHi Setup
AppLaunched=RunInstaller.cmd
PostInstallCmd=<None>
AdminQuietInstCmd=RunInstaller.cmd
UserQuietInstCmd=RunInstaller.cmd
SourceFiles=SourceFiles

[Strings]
InstallPrompt=Install eSureHi for the Sulop setup simulation?
DisplayLicense=
FinishMessage=eSureHi setup has finished.
FILE0=Install-eSureHi.ps1
FILE1=RunInstaller.cmd
FILE2=README-Sulop-Setup.txt
FILE3=payload.zip

[SourceFiles]
SourceFiles0=$stageDir\

[SourceFiles0]
%FILE0%=
%FILE1%=
%FILE2%=
%FILE3%=
"@

Set-Content -Path $sedPath -Value $sed -Encoding ASCII

if (-not $SkipIExpress) {
    $iexpress = Join-Path $env:WINDIR 'System32\iexpress.exe'
    if (Test-Path -LiteralPath $iexpress) {
        Write-Host "Building setup executable..."
        & $iexpress /N $sedPath
    } else {
        Write-Warning "IExpress was not found. Staging folder and payload.zip were still updated."
    }
}

Write-Host "Installer staging updated: $stageDir"
Write-Host "Setup executable target: $setupExe"
