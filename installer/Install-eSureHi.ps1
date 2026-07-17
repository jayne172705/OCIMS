param(
    [switch]$LaunchAfterInstall,
    [switch]$ResetConfig
)

$ErrorActionPreference = 'Stop'

$appName = 'eSureHi'
$publisher = 'Municipality of Sulop'
$configFiles = @('eSureHiConfig.txt', 'GgmsConfig.txt', 'CrsConfig.txt')
$payload = Join-Path $PSScriptRoot 'payload.zip'
$extractRoot = Join-Path $env:TEMP 'eSureHi-installer-payload'
$source = Join-Path $extractRoot 'app'
$installRoot = Join-Path $env:ProgramFiles $appName
$exePath = Join-Path $installRoot 'eSureHi.exe'

if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $elevatedArgs = @(
        '-ExecutionPolicy', 'Bypass',
        '-File', "`"$PSCommandPath`""
    )
    if ($LaunchAfterInstall) { $elevatedArgs += '-LaunchAfterInstall' }
    if ($ResetConfig) { $elevatedArgs += '-ResetConfig' }

    Start-Process -FilePath 'powershell.exe' -ArgumentList $elevatedArgs -Verb RunAs
    exit
}

if (-not (Test-Path -LiteralPath $payload)) {
    throw "Installer payload is missing: $payload"
}

$existingConfigRoot = Join-Path $env:TEMP 'eSureHi-installer-existing-config'
Remove-Item -LiteralPath $existingConfigRoot -Recurse -Force -ErrorAction SilentlyContinue

if ((Test-Path -LiteralPath $installRoot) -and -not $ResetConfig) {
    New-Item -ItemType Directory -Path $existingConfigRoot -Force | Out-Null
    foreach ($configFile in $configFiles) {
        $existingConfig = Join-Path $installRoot $configFile
        if (Test-Path -LiteralPath $existingConfig) {
            Copy-Item -LiteralPath $existingConfig -Destination (Join-Path $existingConfigRoot $configFile) -Force
        }
    }
}

Get-Process -Name $appName -ErrorAction SilentlyContinue | Stop-Process -Force

Remove-Item -LiteralPath $extractRoot -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $extractRoot -Force | Out-Null
Expand-Archive -LiteralPath $payload -DestinationPath $extractRoot -Force

if (-not (Test-Path -LiteralPath $source)) {
    throw "Installer payload does not contain the expected app folder: $source"
}

if (-not (Test-Path -LiteralPath (Join-Path $source 'eSureHi.exe'))) {
    throw "Installer payload does not contain eSureHi.exe"
}

New-Item -ItemType Directory -Path $installRoot -Force | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $installRoot -Recurse -Force

$usersSid = New-Object System.Security.Principal.SecurityIdentifier 'S-1-5-32-545'
$usersAccount = $usersSid.Translate([System.Security.Principal.NTAccount]).Value
& icacls.exe $installRoot /grant "${usersAccount}:(OI)(CI)M" /T | Out-Null

if ((Test-Path -LiteralPath $existingConfigRoot) -and -not $ResetConfig) {
    foreach ($configFile in $configFiles) {
        $savedConfig = Join-Path $existingConfigRoot $configFile
        if (Test-Path -LiteralPath $savedConfig) {
            Copy-Item -LiteralPath $savedConfig -Destination (Join-Path $installRoot $configFile) -Force
        }
    }
}

$shell = New-Object -ComObject WScript.Shell

$desktopShortcut = Join-Path ([Environment]::GetFolderPath('CommonDesktopDirectory')) "$appName.lnk"
$shortcut = $shell.CreateShortcut($desktopShortcut)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installRoot
$shortcut.IconLocation = $exePath
$shortcut.Description = "$appName - $publisher"
$shortcut.Save()

$startMenuDir = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) $appName
New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
$startShortcut = Join-Path $startMenuDir "$appName.lnk"
$shortcut = $shell.CreateShortcut($startShortcut)
$shortcut.TargetPath = $exePath
$shortcut.WorkingDirectory = $installRoot
$shortcut.IconLocation = $exePath
$shortcut.Description = "$appName - $publisher"
$shortcut.Save()

$uninstallScript = Join-Path $installRoot 'Uninstall-eSureHi.ps1'
@"
`$ErrorActionPreference = 'Stop'
`$installRoot = '$installRoot'
Remove-Item -LiteralPath '$desktopShortcut' -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath '$startMenuDir' -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath `$installRoot -Recurse -Force -ErrorAction SilentlyContinue
"@ | Set-Content -Path $uninstallScript -Encoding UTF8

Write-Host "$appName installed successfully to $installRoot"

if ($LaunchAfterInstall -and (Test-Path -LiteralPath $exePath)) {
    Start-Process -FilePath $exePath -WorkingDirectory $installRoot
}
