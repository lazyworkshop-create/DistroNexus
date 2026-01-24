#define MyAppName "DistroNexus - The WSL Distro Manager"
#ifndef MyAppVersion
  #define MyAppVersion "1.0.2"
#endif
#define MyAppPublisher "DistroNexus Team"
#define MyAppURL "https://github.com/DistroNexus/DistroNexus"
#define MyAppExeName "DistroNexus.exe"

[Setup]
; NOTE: The value of AppId uniquely identifies this application. Do not use the same AppId value in installers for other applications.
; (To generate a new GUID, click Tools | Generate GUID inside the IDE.)
AppId={{D157R0-N3XU-5-APP-ID-G3N3R4T3D}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
;AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\DistroNexus
DefaultGroupName=DistroNexus
AllowNoIcons=yes
LicenseFile=assets\license.txt
; Remove the following line to run in administrative install mode (install for all users.)
PrivilegesRequired=admin
OutputDir=..\..\release
OutputBaseFilename=DistroNexus_Setup_{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; Executable
Source: "..\..\build\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion

; Scripts
Source: "..\..\scripts\*"; DestDir: "{app}\scripts"; Flags: ignoreversion recursesubdirs createallsubdirs

; Configuration
; Install distros.json always
Source: "..\..\config\distros.json"; DestDir: "{app}\config"; Flags: ignoreversion
; Install settings.json ONLY if it doesn't exist, to preserve user settings during upgrade
Source: "..\..\config\settings.json"; DestDir: "{app}\config"; Flags: onlyifdoesntexist uninsneveruninstall

; Documentation
Source: "..\..\README.md"; DestDir: "{app}"; Flags: isreadme
Source: "..\..\README_CN.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\docs\release_notes\v{#MyAppVersion}.md"; DestDir: "{app}"; DestName: "RELEASE_NOTES.md"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,DistroNexus}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
