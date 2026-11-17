; Keep AppId and the current-user install scope stable across every release.
#ifndef AppVersion
  #define AppVersion "1.0.4"
#endif
#ifndef AppFileVersion
  #define AppFileVersion "1.0.4.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\1.0.4\win-x64"
#endif
#ifndef InstallerOutputDir
  #define InstallerOutputDir "..\artifacts\installer\1.0.4"
#endif

[Setup]
AppId={{FEC64C79-9E1B-4B88-A5B1-A0FD24DF8313}
AppName=ScreenshotTool
AppVersion={#AppVersion}
AppVerName=ScreenshotTool {#AppVersion}
AppPublisher=ScreenshotTool
VersionInfoVersion={#AppFileVersion}
DefaultDirName={localappdata}\Programs\ScreenshotTool
DefaultGroupName=ScreenshotTool
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.19045
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
OutputDir={#InstallerOutputDir}
OutputBaseFilename=ScreenshotTool-Setup-x64
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\ScreenshotTool.exe
CloseApplications=yes
CloseApplicationsFilter=ScreenshotTool.exe
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "chinesesimp"; MessagesFile: "Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userprograms}\ScreenshotTool\ScreenshotTool"; Filename: "{app}\ScreenshotTool.exe"; WorkingDir: "{app}"
Name: "{userprograms}\ScreenshotTool\{cm:UninstallProgram,ScreenshotTool}"; Filename: "{uninstallexe}"
Name: "{userdesktop}\ScreenshotTool"; Filename: "{app}\ScreenshotTool.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\ScreenshotTool.exe"; Description: "{cm:LaunchProgram,ScreenshotTool}"; Flags: nowait postinstall skipifsilent

; There are intentionally no wildcard uninstall-delete entries. Settings live
; outside {app}, and neither upgrades nor uninstall remove settings or exports.

[Code]
procedure RequestApplicationShutdown;
var
  ExitCode: Integer;
  ApplicationPath: String;
begin
  ApplicationPath := ExpandConstant('{app}\ScreenshotTool.exe');
  if FileExists(ApplicationPath) then begin
    if not Exec(ApplicationPath, '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, ExitCode) then
      Log('Could not request application shutdown; Restart Manager will check for files in use.');
  end;
end;

#include "NativeFolderPicker.iss"

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  RequestApplicationShutdown;
  Result := '';
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  StartupCommand: String;
begin
  if CurUninstallStep = usUninstall then begin
    RequestApplicationShutdown;
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ScreenshotTool', StartupCommand) then
      if CompareText(StartupCommand, '"' + ExpandConstant('{app}\ScreenshotTool.exe') + '"') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'ScreenshotTool');
  end;
end;
