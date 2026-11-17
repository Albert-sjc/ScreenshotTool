; This program exits from InitializeSetup before any wizard or installation.
; Expected process exit code is 1; its result file must contain PASS.
#define NativeFolderPickerProbe
#ifndef ProbeOutputDir
  #define ProbeOutputDir "..\..\.tools\folder-picker-probe"
#endif

[Setup]
AppName=ScreenshotTool Native Folder Picker Probe
AppVersion=1.0
CreateAppDir=no
Uninstallable=no
PrivilegesRequired=lowest
UseSetupLdr=no
OutputDir={#ProbeOutputDir}
OutputBaseFilename=NativeFolderPickerProbe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinesesimp"; MessagesFile: "..\Languages\ChineseSimplified.isl"

#include "..\NativeFolderPicker.iss"

[Code]
function InitializeSetup: Boolean;
var
  Folder, ResultPath, Text: String;
  Lines: TArrayOfString;
begin
  ResultPath := ExpandConstant('{param:result}');
  try
    Folder := ExpandConstant('{param:folder|{tmp}}');
    Text := 'PASS: ' + ProbeNativeFolderPicker(Folder);
  except
    Text := 'FAIL: ' + GetExceptionMessage;
  end;
  SetArrayLength(Lines, 1);
  Lines[0] := Text;
  SaveStringsToUTF8File(ResultPath, Lines, False);
  Result := False;
end;
