; Inno Setup script for AorusLcd - builds a standard Windows installer (setup.exe)
; with a Program Files install, Start Menu shortcut, and Add/Remove Programs entry.
;
; Build:  iscc /DMyAppVersion=1.2.3 installer\AorusLcd.iss
; Expects the self-contained GUI publish (GUI exe + bundled service exe) at
; publish\self-contained\ (see release workflow / README).

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#define MyAppName "AorusLcd"
#define MyAppPublisher "JustinMDotNet"
#define MyAppURL "https://github.com/JustinMDotNet/AorusLcd"
#define MyAppExeName "AorusLcd.Gui.exe"
#define MyServiceName "AorusLcdFeed"

[Setup]
; A stable AppId ties upgrades and the Add/Remove Programs entry together across versions.
AppId={{0BE940C9-EACC-4BBC-8C28-4631D8B4296D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
OutputBaseFilename=AorusLcd-{#MyAppVersion}-setup
OutputDir=output
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
; Installing into Program Files and cleaning up the service on uninstall need admin.
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\src\AorusLcd.Gui\Assets\aoruslcd.ico

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Package the entire self-contained publish output (GUI exe + bundled service exe).
; PDBs shipped by native packages (SkiaSharp/HarfBuzz) are excluded to keep the installer lean.
Source: "..\publish\self-contained\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Stop and remove the background service (if the user installed it) so uninstall is clean.
Filename: "{sys}\sc.exe"; Parameters: "stop {#MyServiceName}"; Flags: runhidden; RunOnceId: "StopAorusLcdFeed"
Filename: "{sys}\sc.exe"; Parameters: "delete {#MyServiceName}"; Flags: runhidden; RunOnceId: "DeleteAorusLcdFeed"
