#ifndef AppVersion
  #error AppVersion must be supplied by scripts/prepare-release.ps1
#endif
#ifndef PublishDir
  #error PublishDir must be supplied by scripts/prepare-release.ps1
#endif
#ifndef ReleaseDir
  #error ReleaseDir must be supplied by scripts/prepare-release.ps1
#endif

[Setup]
; Keep this ID unchanged across upgrades. WinGet uses its uninstall key.
AppId={{B1293760-1F68-47D9-9000-F2B4071C66AE}
AppName=PowerMouse
AppVersion={#AppVersion}
AppVerName=PowerMouse {#AppVersion}
AppPublisher=PowerMouse contributors
AppPublisherURL=https://github.com/agsoto/PowerMouse
AppSupportURL=https://github.com/agsoto/PowerMouse/issues
AppUpdatesURL=https://github.com/agsoto/PowerMouse/releases
VersionInfoVersion={#AppVersion}.0
DefaultDirName={localappdata}\Programs\PowerMouse
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
LicenseFile=..\LICENSE
SetupIconFile=..\src\PowerMouse.App\Assets\PowerMouse.ico
UninstallDisplayIcon={app}\PowerMouse.exe
OutputDir={#ReleaseDir}
OutputBaseFilename=PowerMouse-{#AppVersion}-win-x64-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
AppMutex=Local\PowerMouse.SingleInstance
CloseApplications=no
RestartApplications=no

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "uninstall-startup.ps1"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userprograms}\PowerMouse"; Filename: "{app}\PowerMouse.exe"; WorkingDir: "{app}"

[Run]
Filename: "{app}\PowerMouse.exe"; Parameters: "--show-settings"; Description: "Open PowerMouse"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  ExitCode: Integer;
  Arguments: String;
begin
  if CurUninstallStep = usUninstall then
  begin
    { Run after confirmation and the mutex check, but before deleting files.
      A failure here aborts uninstall so an elevated startup entry cannot dangle. }
    Arguments := '-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
      ExpandConstant('{app}\uninstall-startup.ps1') + '" -InstallPath "' +
      ExpandConstant('{app}') + '"';
    if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
      Arguments, '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      RaiseException('PowerMouse startup cleanup could not start. Uninstall was stopped.');
    if ExitCode <> 0 then
      RaiseException('PowerMouse startup cleanup failed. Disable Start with Windows in PowerMouse and retry.');
  end;
end;
