param(
    [switch]$LaunchAfterInstall,
    [switch]$ResetConfig,
    [switch]$Silent,
    [ValidateSet('Network', 'Online')]
    [string]$Mode = 'Network',
    # Optional overrides (IMS = main app DB)
    [string]$ImsServer = '',
    [string]$ImsPort = '',
    [string]$ImsDatabase = '',
    [string]$ImsUser = '',
    [string]$ImsPassword = '',
    [string]$GgmsServer = '',
    [string]$GgmsPort = '',
    [string]$GgmsDatabase = '',
    [string]$GgmsUser = '',
    [string]$GgmsPassword = '',
    [string]$CrsServer = '',
    [string]$CrsPort = '',
    [string]$CrsDatabase = '',
    [string]$CrsUser = '',
    [string]$CrsPassword = ''
)

$ErrorActionPreference = 'Stop'

# ── Single source of truth: KEEP IN SYNC with ──────────────────────────
#   OCIMS/ViewModels/Shared/ConnectionSettingsViewModel.cs
#   OCIMS/ViewModels/Admin/SettingsViewModel.cs
#   OCIMS/Data/DatabaseConfiguration.cs (IMS defaults)
#   OCIMS/Data/SharedConnections.cs (NetworkServer/User/Password/DBs)
# ── Remote network prefilled credentials (office LAN) ──
$NetworkServer = '192.168.0.47'
$NetworkPort = '3306'
$NetworkUser = 'root'
$NetworkPassword = 'network@2026'
$NetworkImsDatabase = 'ims_db'
$NetworkGgmsDatabase = 'ggms_db'
$NetworkCrsDatabase = 'crs_db'
# ── Online (Hostinger cloud) — IMS_DB only. GGMS/CRS stay on network. ──
$OnlineServer = '194.59.164.58'
$OnlinePort = '3306'
$OnlineImsDatabase = 'u621755393_ims'
$OnlineImsUser = 'u621755393_ims_user'
$OnlineImsPassword = 'Ims@2026'

$appName = 'eSureHi'
$publisher = 'Municipality of Sulop'
$configFiles = @('eSureHiConfig.txt', 'GgmsConfig.txt', 'CrsConfig.txt')
$payload = Join-Path $PSScriptRoot 'payload.zip'
$extractRoot = Join-Path $env:TEMP 'eSureHi-installer-payload'
$source = Join-Path $extractRoot 'app'
$installRoot = Join-Path $env:ProgramFiles $appName
$exePath = Join-Path $installRoot 'eSureHi.exe'

function Get-Preset([string]$mode) {
    if ($mode -eq 'Online') {
        return @{
            ImsServer = $script:OnlineServer; ImsPort = $script:OnlinePort
            ImsDatabase = $script:OnlineImsDatabase; ImsUser = $script:OnlineImsUser
            ImsPassword = $script:OnlineImsPassword
            GgmsServer = $script:NetworkServer; GgmsPort = $script:NetworkPort
            GgmsDatabase = $script:NetworkGgmsDatabase; GgmsUser = $script:NetworkUser
            GgmsPassword = $script:NetworkPassword
            CrsServer = $script:NetworkServer; CrsPort = $script:NetworkPort
            CrsDatabase = $script:NetworkCrsDatabase; CrsUser = $script:NetworkUser
            CrsPassword = $script:NetworkPassword
        }
    }
    return @{
        ImsServer = $script:NetworkServer; ImsPort = $script:NetworkPort
        ImsDatabase = $script:NetworkImsDatabase; ImsUser = $script:NetworkUser
        ImsPassword = $script:NetworkPassword
        GgmsServer = $script:NetworkServer; GgmsPort = $script:NetworkPort
        GgmsDatabase = $script:NetworkGgmsDatabase; GgmsUser = $script:NetworkUser
        GgmsPassword = $script:NetworkPassword
        CrsServer = $script:NetworkServer; CrsPort = $script:NetworkPort
        CrsDatabase = $script:NetworkCrsDatabase; CrsUser = $script:NetworkUser
        CrsPassword = $script:NetworkPassword
    }
}

function Read-ConfigFile([string]$path) {
    $map = @{}
    if (Test-Path -LiteralPath $path) {
        foreach ($line in (Get-Content -LiteralPath $path)) {
            $idx = $line.IndexOf('=')
            if ($idx -gt 0) { $map[$line.Substring(0, $idx).Trim()] = $line.Substring($idx + 1).Trim() }
        }
    }
    return $map
}

function Write-ConfigFile([string]$path, [string]$server, [string]$port, [string]$database, [string]$user, [string]$password) {
    @(
        "Server=$server"
        "Port=$port"
        "Database=$database"
        "User=$user"
        "Password=$password"
    ) | Set-Content -LiteralPath $path -Encoding UTF8
}

function Test-TcpPort([string]$server, [string]$port) {
    try {
        $p = [int]$port
        $client = New-Object Net.Sockets.TcpClient
        $iar = $client.BeginConnect($server.Trim(), $p, $null, $null)
        if ($iar.AsyncWaitHandle.WaitOne(4000)) { $client.EndConnect($iar); $client.Close(); return $true }
        $client.Close(); return $false
    } catch { return $false }
}

function Show-DbConfigDialog([hashtable]$initial, [string]$mode) {
    Add-Type -AssemblyName System.Windows.Forms
    Add-Type -AssemblyName System.Drawing

    $form = New-Object Windows.Forms.Form
    $form.Text = "$appName Setup - Database Connection (prefilled, editable)"
    $form.Size = New-Object Drawing.Size(560, 640)
    $form.StartPosition = 'CenterScreen'
    $form.FormBorderStyle = 'FixedDialog'
    $form.MaximizeBox = $false
    $form.MinimizeBox = $false
    $form.TopMost = $true

    $y = 10
    $lbl = New-Object Windows.Forms.Label
    $lbl.Text = 'Prefilled credentials below match the app (Network = office LAN, Online = Hostinger cloud for IMS_DB). Edit if needed, then Install.'
    $lbl.Location = New-Object Drawing.Point(12, $y)
    $lbl.Size = New-Object Drawing.Size(520, 32)
    $form.Controls.Add($lbl)
    $y += 38

    $rbNetwork = New-Object Windows.Forms.RadioButton
    $rbNetwork.Text = 'Network (LAN 192.168.0.47 - ims_db / ggms_db / crs_db)'
    $rbNetwork.Location = New-Object Drawing.Point(12, $y)
    $rbNetwork.Size = New-Object Drawing.Size(520, 20)
    $rbNetwork.Checked = ($mode -ne 'Online')
    $form.Controls.Add($rbNetwork)
    $y += 22
    $rbOnline = New-Object Windows.Forms.RadioButton
    $rbOnline.Text = 'Online (Hostinger 194.59.164.58 - IMS_DB only, GGMS/CRS stay on LAN)'
    $rbOnline.Location = New-Object Drawing.Point(12, $y)
    $rbOnline.Size = New-Object Drawing.Size(520, 20)
    $rbOnline.Checked = ($mode -eq 'Online')
    $form.Controls.Add($rbOnline)
    $y += 28

    $boxes = @{}
    function Add-Group([string]$title, [string]$prefix, [int]$top) {
        $gb = New-Object Windows.Forms.GroupBox
        $gb.Text = $title
        $gb.Location = New-Object Drawing.Point(12, $top)
        $gb.Size = New-Object Drawing.Size(520, 140)
        $form.Controls.Add($gb)
        $fields = @('Server', 'Port', 'Database', 'User', 'Password')
        $x = 10
        foreach ($f in $fields) {
            $l = New-Object Windows.Forms.Label
            $l.Text = $f
            $l.Location = New-Object Drawing.Point($x, 22)
            $l.Size = New-Object Drawing.Size(90, 14)
            $gb.Controls.Add($l)
            $t = New-Object Windows.Forms.TextBox
            $t.Name = "$prefix$f"
            $t.Location = New-Object Drawing.Point($x, 38)
            $t.Size = New-Object Drawing.Size(90, 22)
            if ($f -eq 'Password') { $t.UseSystemPasswordChar = $true }
            if ($f -eq 'Port') { $t.Size = New-Object Drawing.Size(60, 22) }
            if ($f -eq 'Server') { $t.Size = New-Object Drawing.Size(130, 22) }
            $t.Text = [string]$initial["$prefix$f"]
            $gb.Controls.Add($t)
            $boxes["$prefix$f"] = $t
            $x += ($t.Size.Width + 8)
        }
        $hint = New-Object Windows.Forms.Label
        $hint.ForeColor = [Drawing.Color]::Gray
        $hint.Location = New-Object Drawing.Point(10, 68)
        $hint.Size = New-Object Drawing.Size(500, 60)
        if ($prefix -eq 'Ims') { $hint.Text = "Default Network: $NetworkServer / $NetworkImsDatabase / $NetworkUser`r`nDefault Online: $OnlineServer / $OnlineImsDatabase / $OnlineImsUser" }
        elseif ($prefix -eq 'Ggms') { $hint.Text = "Default: $NetworkServer / $NetworkGgmsDatabase / $NetworkUser (always LAN, editable)" }
        else { $hint.Text = "Default: $NetworkServer / $NetworkCrsDatabase / $NetworkUser (always LAN, editable)" }
        $gb.Controls.Add($hint)
    }

    Add-Group 'IMS_DB (main app database)' 'Ims' $y; $y += 146
    Add-Group 'GGMS_DB (budget / consolidated_transactions)' 'Ggms' $y; $y += 146
    Add-Group 'CRS_DB (validated beneficiaries)' 'Crs' $y; $y += 146

    $status = New-Object Windows.Forms.Label
    $status.Location = New-Object Drawing.Point(12, $y)
    $status.Size = New-Object Drawing.Size(520, 20)
    $status.ForeColor = [Drawing.Color]::DarkGreen
    $form.Controls.Add($status)
    $y += 24

    $btnTest = New-Object Windows.Forms.Button
    $btnTest.Text = 'Test'
    $btnTest.Location = New-Object Drawing.Point(12, $y)
    $btnTest.Size = New-Object Drawing.Size(100, 30)
    $btnTest.Add_Click({
        $ok1 = Test-TcpPort $boxes['ImsServer'].Text $boxes['ImsPort'].Text
        $ok2 = Test-TcpPort $boxes['GgmsServer'].Text $boxes['GgmsPort'].Text
        $ok3 = Test-TcpPort $boxes['CrsServer'].Text $boxes['CrsPort'].Text
        $status.Text = "TCP reachability - IMS:$ok1 GGMS:$ok2 CRS:$ok3"
    })
    $form.Controls.Add($btnTest)

    $btnOk = New-Object Windows.Forms.Button
    $btnOk.Text = 'Install'
    $btnOk.DialogResult = 'OK'
    $btnOk.Location = New-Object Drawing.Point(352, $y)
    $btnOk.Size = New-Object Drawing.Size(90, 30)
    $form.Controls.Add($btnOk)

    $btnCancel = New-Object Windows.Forms.Button
    $btnCancel.Text = 'Cancel'
    $btnCancel.DialogResult = 'Cancel'
    $btnCancel.Location = New-Object Drawing.Point(448, $y)
    $btnCancel.Size = New-Object Drawing.Size(84, 30)
    $form.Controls.Add($btnCancel)

    $form.AcceptButton = $btnOk
    $form.CancelButton = $btnCancel

    $applyPreset = {
        param([string]$m)
        $p = Get-Preset $m
        # Online only changes IMS; GGMS/CRS always reset to network preset but stay editable after.
        $boxes['ImsServer'].Text = $p.ImsServer; $boxes['ImsPort'].Text = $p.ImsPort
        $boxes['ImsDatabase'].Text = $p.ImsDatabase; $boxes['ImsUser'].Text = $p.ImsUser
        $boxes['ImsPassword'].Text = $p.ImsPassword
        $boxes['GgmsServer'].Text = $p.GgmsServer; $boxes['GgmsPort'].Text = $p.GgmsPort
        $boxes['GgmsDatabase'].Text = $p.GgmsDatabase; $boxes['GgmsUser'].Text = $p.GgmsUser
        $boxes['GgmsPassword'].Text = $p.GgmsPassword
        $boxes['CrsServer'].Text = $p.CrsServer; $boxes['CrsPort'].Text = $p.CrsPort
        $boxes['CrsDatabase'].Text = $p.CrsDatabase; $boxes['CrsUser'].Text = $p.CrsUser
        $boxes['CrsPassword'].Text = $p.CrsPassword
        $status.Text = "$m preset applied to textboxes (still editable)."
    }
    $rbNetwork.Add_CheckedChanged({ if ($rbNetwork.Checked) { & $applyPreset 'Network' } })
    $rbOnline.Add_CheckedChanged({ if ($rbOnline.Checked) { & $applyPreset 'Online' } })

    $result = $form.ShowDialog()
    if ($result -ne 'OK') { return $null }
    $out = @{}
    foreach ($k in $boxes.Keys) { $out[$k] = $boxes[$k].Text.Trim() }
    $out['Mode'] = if ($rbOnline.Checked) { 'Online' } else { 'Network' }
    $form.Dispose()
    return $out
}

# ── Elevate ──
if (-not ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    $elevatedArgs = @('-ExecutionPolicy', 'Bypass', '-File', "`"$PSCommandPath`"", '-Mode', $Mode)
    if ($LaunchAfterInstall) { $elevatedArgs += '-LaunchAfterInstall' }
    if ($ResetConfig) { $elevatedArgs += '-ResetConfig' }
    if ($Silent) { $elevatedArgs += '-Silent' }
    Start-Process -FilePath 'powershell.exe' -ArgumentList $elevatedArgs -Verb RunAs
    exit
}

if (-not (Test-Path -LiteralPath $payload)) {
    throw "Installer payload is missing: $payload"
}

# ── Stage previous configs (upgrade preserve) ──
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

# ── Decide DB credentials to write ──
# Priority: explicit CLI overrides > installer dialog result > preserved existing > Mode preset > payload default.
$preset = Get-Preset $Mode
$initial = @{
    ImsServer = $preset.ImsServer; ImsPort = $preset.ImsPort; ImsDatabase = $preset.ImsDatabase
    ImsUser = $preset.ImsUser; ImsPassword = $preset.ImsPassword
    GgmsServer = $preset.GgmsServer; GgmsPort = $preset.GgmsPort; GgmsDatabase = $preset.GgmsDatabase
    GgmsUser = $preset.GgmsUser; GgmsPassword = $preset.GgmsPassword
    CrsServer = $preset.CrsServer; CrsPort = $preset.CrsPort; CrsDatabase = $preset.CrsDatabase
    CrsUser = $preset.CrsUser; CrsPassword = $preset.CrsPassword
}

# Seed initial values from preserved existing install (so upgrade keeps working creds in the textboxes).
if ((Test-Path -LiteralPath $existingConfigRoot) -and -not $ResetConfig) {
    $e = Read-ConfigFile (Join-Path $existingConfigRoot 'eSureHiConfig.txt')
    if ($e.Count -gt 0) {
        if ($e['Server']) { $initial.ImsServer = $e['Server'] }
        if ($e['Port']) { $initial.ImsPort = $e['Port'] }
        if ($e['Database']) { $initial.ImsDatabase = $e['Database'] }
        if ($e['User']) { $initial.ImsUser = $e['User'] }
        if ($e['Password']) { $initial.ImsPassword = $e['Password'] }
    }
    $g = Read-ConfigFile (Join-Path $existingConfigRoot 'GgmsConfig.txt')
    if ($g.Count -gt 0) {
        if ($g['Server']) { $initial.GgmsServer = $g['Server'] }
        if ($g['Port']) { $initial.GgmsPort = $g['Port'] }
        if ($g['Database']) { $initial.GgmsDatabase = $g['Database'] }
        if ($g['User']) { $initial.GgmsUser = $g['User'] }
        if ($g['Password']) { $initial.GgmsPassword = $g['Password'] }
    }
    $c = Read-ConfigFile (Join-Path $existingConfigRoot 'CrsConfig.txt')
    if ($c.Count -gt 0) {
        if ($c['Server']) { $initial.CrsServer = $c['Server'] }
        if ($c['Port']) { $initial.CrsPort = $c['Port'] }
        if ($c['Database']) { $initial.CrsDatabase = $c['Database'] }
        if ($c['User']) { $initial.CrsUser = $c['User'] }
        if ($c['Password']) { $initial.CrsPassword = $c['Password'] }
    }
}

# Explicit CLI overrides win over everything.
if ($ImsServer) { $initial.ImsServer = $ImsServer }
if ($ImsPort) { $initial.ImsPort = $ImsPort }
if ($ImsDatabase) { $initial.ImsDatabase = $ImsDatabase }
if ($ImsUser) { $initial.ImsUser = $ImsUser }
if ($ImsPassword) { $initial.ImsPassword = $ImsPassword }
if ($GgmsServer) { $initial.GgmsServer = $GgmsServer }
if ($GgmsPort) { $initial.GgmsPort = $GgmsPort }
if ($GgmsDatabase) { $initial.GgmsDatabase = $GgmsDatabase }
if ($GgmsUser) { $initial.GgmsUser = $GgmsUser }
if ($GgmsPassword) { $initial.GgmsPassword = $GgmsPassword }
if ($CrsServer) { $initial.CrsServer = $CrsServer }
if ($CrsPort) { $initial.CrsPort = $CrsPort }
if ($CrsDatabase) { $initial.CrsDatabase = $CrsDatabase }
if ($CrsUser) { $initial.CrsUser = $CrsUser }
if ($CrsPassword) { $initial.CrsPassword = $CrsPassword }

$final = $initial
$hasOverride = $ImsServer -or $ImsPort -or $ImsDatabase -or $ImsUser -or $ImsPassword -or `
    $GgmsServer -or $GgmsPort -or $GgmsDatabase -or $GgmsUser -or $GgmsPassword -or `
    $CrsServer -or $CrsPort -or $CrsDatabase -or $CrsUser -or $CrsPassword

if (-not $Silent -and -not $hasOverride) {
    $dlg = Show-DbConfigDialog $initial $Mode
    if ($null -eq $dlg) { Write-Host 'Install cancelled by user.'; exit 1 }
    $final = $dlg
}

Write-ConfigFile (Join-Path $installRoot 'eSureHiConfig.txt') $final.ImsServer $final.ImsPort $final.ImsDatabase $final.ImsUser $final.ImsPassword
Write-ConfigFile (Join-Path $installRoot 'GgmsConfig.txt') $final.GgmsServer $final.GgmsPort $final.GgmsDatabase $final.GgmsUser $final.GgmsPassword
Write-ConfigFile (Join-Path $installRoot 'CrsConfig.txt') $final.CrsServer $final.CrsPort $final.CrsDatabase $final.CrsUser $final.CrsPassword

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
Write-Host "DB configs written (prefilled $Mode preset, editable in app via gear icon / Settings)."

if ($LaunchAfterInstall -and (Test-Path -LiteralPath $exePath)) {
    Start-Process -FilePath $exePath -WorkingDirectory $installRoot
}
