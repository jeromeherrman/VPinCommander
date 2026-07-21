; Inno Setup script for VPin Commander.
; Build: iscc /DMyAppVersion=0.9.0 installer\VPinCommander.iss
; Produces a per-user installer (no admin/UAC prompt) so the in-app updater
; can run it silently. Source files come from the `publish` folder.

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#define MyAppName "VPin Commander"
#define MyAppPublisher "Jerome Herrman"
#define MyAppUrl "https://github.com/jeromeherrman/VPinCommander"
#define MyAppExeName "VPinCommander.exe"
#define MyAppSrc "..\publish"

[Setup]
; Stable identity across versions — do not change once shipped.
AppId={{8F2A6C41-9B3D-4E7A-8C15-2D9E0F4B7A63}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; Per-user install: no elevation, so silent updates never hit a UAC prompt.
PrivilegesRequired=lowest
OutputDir=..\installer-out
OutputBaseFilename=VPinCommander-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
; Let the restart manager close a running instance so files can be replaced.
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#MyAppSrc}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; No skipifsilent: interactive installs show a "Launch" checkbox, and silent
; installs (the in-app updater) relaunch the app automatically.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall
