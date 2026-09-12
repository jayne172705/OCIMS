#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "eSureHi"
#define MyAppPublisher "Municipality of Sulop"
#define MyAppExeName "eSureHi.exe"
#define PublishDir "..\release\eSureHi-publish"

[Setup]
AppId={{E88F5C21-1E29-4D66-A6FA-A6BC62EC2D2B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=eSureHi-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Excludes: "eSureHiConfig.txt,GgmsConfig.txt,CrsConfig.txt"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PublishDir}\eSureHiConfig.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist
Source: "{#PublishDir}\GgmsConfig.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist
Source: "{#PublishDir}\CrsConfig.txt"; DestDir: "{app}"; Flags: onlyifdoesntexist skipifsourcedoesntexist

[Icons]
Name: "{autoprograms}\{#MyAppName}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{cmd}"; Parameters: "/C icacls ""{app}"" /grant *S-1-5-32-545:(OI)(CI)M /T /C"; Flags: runhidden waituntilterminated
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
