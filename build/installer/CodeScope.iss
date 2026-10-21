; ---------------------------------------------------------------------------
; CodeScope — Windows installer
;
; Built by build\build-installer.ps1, which publishes the application, makes
; sure the .NET 8 desktop runtime installer is present, and then calls the
; Inno Setup compiler. The result is a single Setup.exe that installs the
; application for the current user, without needing an administrator, and
; adds a proper uninstaller.
;
; The installer looks for the .NET 8 desktop runtime on the machine. When it
; is there, the application is all that gets installed. When it is missing,
; the installer offers to add it from the copy that ships inside the setup,
; so the application works straight away.
; ---------------------------------------------------------------------------

#define MyAppName "CodeScope"
#define MyAppVersion "0.1.0"
#define MyAppPublisher "the CodeScope contributors"
#define MyAppUrl "https://github.com/<owner>/CodeScope"
#define MyAppExeName "CodeScope.exe"
#define MyAppId "{{B7A9C1E4-3D52-4C8A-9E11-6F2D5A0B7C34}"
#define MyRuntimeVersion "8.0.31"
#define MyRuntimeFile "runtime\windowsdesktop-runtime-8.0.31-win-x64.exe"

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
VersionInfoVersion={#MyAppVersion}
VersionInfoDescription={#MyAppName} installer

; The default install is per-user, so no administrator rights are needed and
; nothing is written outside the user's own profile. A user who wants a
; machine-wide install can choose it in the wizard.
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0

OutputBaseFilename=CodeScope-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no
SetupIconFile=..\..\src\CodeScope.App\Assets\CodeScope.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\..\LICENSE
InfoBeforeFile=..\installer\readme.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

; Checked on a first install and left alone on an upgrade, so the runtime is
; not reinstalled over an existing one. The installer still checks whether
; the runtime is really there before doing anything.
Name: "dotnetruntime"; Description: "Install the .NET 8 desktop runtime (x64) - untick this if the runtime is already on this computer"; Flags: checkedonce

[Files]
; The published application, exactly as it is tested.
Source: "..\..\publish\CodeScope\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; The runtime installer, unpacked only when the user asks for it and the
; runtime really is missing.
Source: "{#MyRuntimeFile}"; DestDir: "{tmp}"; Flags: deleteafterinstall; Check: RuntimeNeeded

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
; The application, when the user asks for it on the final page. The runtime
; has already been installed by then.
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Only what this installer put there. The settings and the analysis history
; live in %LOCALAPPDATA%\CodeScope and are left alone.
Type: filesandordirs; Name: "{app}"

[Code]
const
  RuntimeDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/8.0';

var
  RuntimeFound: Boolean;

{ Is a .NET 8 desktop runtime registered under this key? The installed
  versions are the value names, each one a flag. }
function HasRuntimeVersion(const RootKey: Integer; const SubKey: String): Boolean;
var
  Names: TArrayOfString;
  I: Integer;
begin
  Result := False;
  if RegGetValueNames(RootKey, SubKey, Names) then
    for I := 0 to GetArrayLength(Names) - 1 do
      if Copy(Names[I], 1, 2) = '8.' then
        Result := True;
end;

{ Is the .NET 8 desktop runtime already on this machine? It is installed per
  machine or per user, and either is good enough. The installer itself is a
  32-bit program, so the machine-wide key is read through the 32-bit view;
  both are checked to be sure. }
function DotNet8DesktopRuntimeInstalled: Boolean;
begin
  Result := HasRuntimeVersion(HKLM,
    'SOFTWARE\dotnet\Setup\InstalledVersions\x64\SharedFX\Microsoft.WindowsDesktop.App');
  if not Result then
    Result := HasRuntimeVersion(HKLM,
      'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\SharedFX\Microsoft.WindowsDesktop.App');
  if not Result then
    Result := HasRuntimeVersion(HKCU,
      'SOFTWARE\dotnet\Setup\InstalledVersions\x64\SharedFX\Microsoft.WindowsDesktop.App');
end;

{ Did the user ask for the runtime, and is it really missing? }
function RuntimeNeeded: Boolean;
begin
  Result := WizardIsTaskSelected('dotnetruntime') and not DotNet8DesktopRuntimeInstalled;
end;

{ Say what was found, so the choice on the next page makes sense. }
function InitializeSetup: Boolean;
var
  Message: String;
begin
  Result := True;
  RuntimeFound := DotNet8DesktopRuntimeInstalled;

  if RuntimeFound then
    Message :=
      'The .NET 8 desktop runtime (x64) is already installed on this computer,' + #13#10 +
      'so CodeScope will run straight away.' + #13#10 + #13#10 +
      'The next page has a box to install a copy of the runtime. Untick it and' + #13#10 +
      'nothing extra is installed.'
  else
    Message :=
      'CodeScope needs the .NET 8 desktop runtime (x64) to run, and it was not found on this' + #13#10 +
      'computer. Windows ships the .NET Framework, which is a different thing and cannot' + #13#10 +
      'run this application.' + #13#10 + #13#10 +
      'This setup carries a copy of the official installer, so it can be added for you.' + #13#10 +
      'The next page has a box for it; leave it ticked to install the runtime, or untick' + #13#10 +
      'it and install it yourself later from:' + #13#10 +
      '  ' + RuntimeDownloadUrl;

  { SuppressibleMsgBox stays quiet during a silent installation. }
  SuppressibleMsgBox(Message, mbInformation, MB_OK, IDOK);
end;

{ Install the runtime that ships inside the setup. }
procedure InstallBundledRuntime;
var
  ResultCode: Integer;
  Installer: String;
begin
  if DotNet8DesktopRuntimeInstalled then
  begin
    { It is already there; nothing to do and no reason to ask for rights. }
    Exit;
  end;

  Installer := ExpandConstant('{tmp}\windowsdesktop-runtime-8.0.31-win-x64.exe');

  if not FileExists(Installer) then
  begin
    MsgBox('The copy of the .NET runtime installer could not be found.' + #13#10 + #13#10 +
      'Please install it manually from:' + #13#10 + '  ' + RuntimeDownloadUrl, mbError, MB_OK);
    Exit;
  end;

  if not Exec(Installer, '/install /quiet /norestart', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('The .NET runtime installer could not be started.' + #13#10 + #13#10 +
      'Please install it manually from:' + #13#10 + '  ' + RuntimeDownloadUrl, mbError, MB_OK);
    Exit;
  end;

  { 3010 means it worked and Windows wants to restart later. }
  if (ResultCode <> 0) and (ResultCode <> 3010) then
    MsgBox('The .NET 8 desktop runtime did not install (exit code ' + IntToStr(ResultCode) + ').' + #13#10 + #13#10 +
      'CodeScope needs it to run. Please install it manually from:' + #13#10 + '  ' + RuntimeDownloadUrl, mbError, MB_OK);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
    if RuntimeNeeded then
      InstallBundledRuntime;
end;
