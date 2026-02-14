; DistroNexus Installer Script for Inno Setup
; Build with: iscc installer.iss
; Requires: Inno Setup 6.0 or later (https://jrsoftware.org/isinfo.php)

#define MyAppName "DistroNexus"
#define MyAppVersion "2.0.1"
#define MyAppPublisher "LazyWorkshop"
#define MyAppURL "https://github.com/lazyworkshop-create/DistroNexus"
#define MyAppExeName "DistroNexus.Desktop.exe"
#define MyAppDescription "WSL Distribution Manager"

[Setup]
; Application information
AppId={{A8B9C0D1-E2F3-4567-8901-2345678ABCDE}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
; Output settings
OutputDir=..\release\installer
OutputBaseFilename=DistroNexus-{#MyAppVersion}-Setup
SetupIconFile=..\assets\icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
; Windows version requirements
MinVersion=10.0.17763
; Privileges
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
; UI settings
WizardStyle=modern
WizardSizePercent=100
; Uninstaller
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application files (from publish output)
Source: "..\release\DistroNexus-v{#MyAppVersion}-Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; Note: Adjust the source path to match your actual publish output directory

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "{#MyAppDescription}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "{#MyAppDescription}"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Registry]
; Add to PATH (optional)
Root: HKLM; Subkey: "SYSTEM\CurrentControlSet\Control\Session Manager\Environment"; ValueType: expandsz; ValueName: "Path"; ValueData: "{olddata};{app}"; Check: NeedsAddPath('{app}')

[Code]
// Check if path needs to be added
function NeedsAddPath(Param: string): boolean;
var
  OrigPath: string;
begin
  if not RegQueryStringValue(HKEY_LOCAL_MACHINE,
    'SYSTEM\CurrentControlSet\Control\Session Manager\Environment',
    'Path', OrigPath)
  then begin
    Result := True;
    exit;
  end;
  // Check if already in path
  Result := Pos(';' + Param + ';', ';' + OrigPath + ';') = 0;
end;

// Custom welcome page message
function UpdateReadyMemo(Space, NewLine, MemoUserInfoInfo, MemoDirInfo, MemoTypeInfo,
  MemoComponentsInfo, MemoGroupInfo, MemoTasksInfo: String): String;
var
  S: String;
begin
  S := '';
  S := S + 'DistroNexus v{#MyAppVersion} Installation' + NewLine + NewLine;
  S := S + 'This will install DistroNexus, a WSL distribution manager.' + NewLine + NewLine;
  
  if MemoDirInfo <> '' then
    S := S + MemoDirInfo + NewLine + NewLine;
    
  if MemoGroupInfo <> '' then
    S := S + MemoGroupInfo + NewLine + NewLine;
    
  if MemoTasksInfo <> '' then
    S := S + MemoTasksInfo + NewLine + NewLine;
    
  S := S + 'Requirements:' + NewLine;
  S := S + Space + '• Windows 10 version 1903 or later' + NewLine;
  S := S + Space + '• WSL enabled' + NewLine;
  S := S + Space + '• .NET 10 Runtime (framework-dependent build)' + NewLine;
  
  Result := S;
end;

// Check for WSL on install
function InitializeSetup(): Boolean;
var
  ResultCode: Integer;
begin
  Result := True;
  
  // Check if WSL is available
  if not Exec('wsl.exe', '--status', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    if MsgBox('WSL does not appear to be installed on this system.' + #13#10 + 
              'DistroNexus requires WSL to function.' + #13#10#13#10 +
              'Do you want to continue the installation anyway?',
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
    end;
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}\config"
Type: filesandordirs; Name: "{app}\logs"
