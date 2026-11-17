[CustomMessages]
; Native Windows Common Item Dialog. Inno 6.7.4 supports IUnknown interfaces;
; see its bundled Examples\CodeAutomation2.iss. No helper process is required.
english.SelectInstallFolder=Choose the folder in which to install ScreenshotTool
english.UseInstallFolder=Select folder
chinesesimp.SelectInstallFolder=选择 ScreenshotTool 的安装位置
chinesesimp.UseInstallFolder=选择文件夹

[Code]
const
  CLSID_NativeFileOpenDialog = '{DC1C5A9C-E88A-4DDE-A5A1-60F82A20AEF7}';
  IID_NativeShellItem = '{43826D1E-E718-42EE-BC55-A1E261C37BFE}';
  FOS_PICKFOLDERS = $00000020;
  FOS_FORCEFILESYSTEM = $00000040;
  FOS_PATHMUSTEXIST = $00000800;
  FOS_NOCHANGEDIR = $00000008;
  FOS_DONTADDTORECENT = $02000000;
  SIGDN_FILESYSPATH = $80058000;
  FDAP_TOP = 1;
  NativeFolderSelected = 0;
  NativeFolderCancelled = 1;
  NativeFolderUnavailable = 2;
  CancelledHResult = -2147023673; { HRESULT_FROM_WIN32(ERROR_CANCELLED) }

type
  { Inno Setup 6 produces a 32-bit setup process, including for x64 apps.
    Cardinal represents its native PWSTR/void*; it is never a managed string. }
  INativeShellItem = interface(IUnknown)
    '{43826D1E-E718-42EE-BC55-A1E261C37BFE}'
    function BindToHandler(Context: Cardinal; var HandlerID: TGUID;
      var InterfaceID: TGUID; out Value: IUnknown): HResult;
    function GetParent(out Value: INativeShellItem): HResult;
    function GetDisplayName(NameKind: DWORD; out Value: Cardinal): HResult;
    function GetAttributes(Mask: DWORD; out Attributes: DWORD): HResult;
    function Compare(Other: INativeShellItem; Hint: DWORD; out Order: Integer): HResult;
  end;

  INativeFileDialog = interface(IUnknown)
    '{42F85136-DB7E-439C-85F1-E4075D135FC8}'
    { IModalWindow and IFileDialog methods must retain their Windows vtable order. }
    function Show(Owner: HWND): HResult;
    function SetFileTypes(Count: UINT; Filters: Cardinal): HResult;
    function SetFileTypeIndex(Index: UINT): HResult;
    function GetFileTypeIndex(out Index: UINT): HResult;
    function Advise(Events: IUnknown; out Cookie: DWORD): HResult;
    function Unadvise(Cookie: DWORD): HResult;
    function SetOptions(Options: DWORD): HResult;
    function GetOptions(out Options: DWORD): HResult;
    function SetDefaultFolder(Folder: INativeShellItem): HResult;
    function SetFolder(Folder: INativeShellItem): HResult;
    function GetFolder(out Folder: INativeShellItem): HResult;
    function GetCurrentSelection(out Item: INativeShellItem): HResult;
    function SetFileName(Name: String): HResult;
    function GetFileName(out Name: Cardinal): HResult;
    function SetTitle(Title: String): HResult;
    function SetOkButtonLabel(LabelText: String): HResult;
    function SetFileNameLabel(LabelText: String): HResult;
    function GetResult(out Item: INativeShellItem): HResult;
    function AddPlace(Folder: INativeShellItem; Placement: DWORD): HResult;
    function SetDefaultExtension(Extension: String): HResult;
    function Close(ResultCode: HResult): HResult;
    function SetClientGuid(var ClientID: TGUID): HResult;
    function ClearClientData: HResult;
    function SetFilter(Filter: IUnknown): HResult;
  end;

function SHCreateItemFromParsingName(Path: String; Context: Cardinal;
  var InterfaceID: TGUID; out Item: IUnknown): HResult;
  external 'SHCreateItemFromParsingName@shell32.dll stdcall delayload';
function NativeStringLength(Text: Cardinal): Integer;
  external 'lstrlenW@kernel32.dll stdcall';
function CopyNativeString(Destination: String; Source: Cardinal; Count: Integer): Cardinal;
  external 'lstrcpynW@kernel32.dll stdcall';
procedure FreeNativeString(Value: Cardinal);
  external 'CoTaskMemFree@ole32.dll stdcall';

function NativeShellItem(const Path: String): INativeShellItem;
var
  InterfaceID: TGUID;
  Item: IUnknown;
begin
  InterfaceID := StringToGuid(IID_NativeShellItem);
  OleCheck(SHCreateItemFromParsingName(Path, 0, InterfaceID, Item));
  Result := INativeShellItem(Item);
end;

function NativeShellItemPath(Item: INativeShellItem): String;
var
  Text: Cardinal;
  Count: Integer;
begin
  Text := 0;
  OleCheck(Item.GetDisplayName(SIGDN_FILESYSPATH, Text));
  try
    Count := NativeStringLength(Text);
    if (Count = 0) or (Count > 32767) then
      RaiseException('The selected folder has no valid file-system path.');
    Result := StringOfChar(#0, Count + 1);
    CopyNativeString(Result, Text, Count + 1);
    SetLength(Result, Count);
  finally
    FreeNativeString(Text);
  end;
end;

function ExistingBrowseFolder(const Directory: String): String;
var
  Parent: String;
begin
  Result := RemoveBackslashUnlessRoot(Directory);
  if CompareText(ExtractFileName(Result), 'ScreenshotTool') = 0 then
    Result := ExtractFileDir(Result);
  while (Result <> '') and not DirExists(Result) do begin
    Parent := ExtractFileDir(RemoveBackslashUnlessRoot(Result));
    if Parent = Result then
      Break;
    Result := Parent;
  end;
  if not DirExists(Result) then
    Result := ExpandConstant('{userdesktop}');
end;

function InstallFolderBelow(const Parent: String): String;
begin
  Result := RemoveBackslashUnlessRoot(Parent);
  if CompareText(ExtractFileName(Result), 'ScreenshotTool') <> 0 then
    Result := AddBackslash(Result) + 'ScreenshotTool';
end;

procedure AddNativeFolderPlace(Dialog: INativeFileDialog; const Folder: String);
begin
  if not DirExists(Folder) then
    Exit;
  try
    OleCheck(Dialog.AddPlace(NativeShellItem(Folder), FDAP_TOP));
  except
    { A missing, redirected or inaccessible optional place must not prevent
      choosing another folder with the normal Windows navigation pane. }
    Log('Optional folder place unavailable: ' + GetExceptionMessage);
  end;
end;

function CreateNativeFolderDialog(const InitialFolder: String): INativeFileDialog;
var
  Instance: IUnknown;
  Options: DWORD;
begin
  Instance := CreateComObject(StringToGuid(CLSID_NativeFileOpenDialog));
  Result := INativeFileDialog(Instance);
  OleCheck(Result.GetOptions(Options));
  OleCheck(Result.SetOptions(Options or FOS_PICKFOLDERS or FOS_FORCEFILESYSTEM or
    FOS_PATHMUSTEXIST or FOS_NOCHANGEDIR or FOS_DONTADDTORECENT));
  OleCheck(Result.SetTitle(CustomMessage('SelectInstallFolder')));
  OleCheck(Result.SetOkButtonLabel(CustomMessage('UseInstallFolder')));
  AddNativeFolderPlace(Result, ExpandConstant('{userdocs}'));
  AddNativeFolderPlace(Result, ExpandConstant('{userdesktop}'));
  if DirExists(InitialFolder) then
    OleCheck(Result.SetFolder(NativeShellItem(InitialFolder)));
end;

function CompleteNativeFolderDialog(Dialog: INativeFileDialog; Status: HResult;
  var Directory: String): Integer;
var
  Item: INativeShellItem;
  Chosen: String;
begin
  if Status = CancelledHResult then begin
    Result := NativeFolderCancelled;
    Exit;
  end;
  OleCheck(Status);
  OleCheck(Dialog.GetResult(Item));
  Chosen := NativeShellItemPath(Item);
  if not DirExists(Chosen) then
    RaiseException('The selected folder is unavailable.');
  Directory := InstallFolderBelow(Chosen);
  Result := NativeFolderSelected;
end;

function BrowseNativeFolder(Owner: HWND; var Directory: String): Integer;
var
  Dialog: INativeFileDialog;
begin
  Result := NativeFolderUnavailable;
  try
    Dialog := CreateNativeFolderDialog(ExistingBrowseFolder(Directory));
    Result := CompleteNativeFolderDialog(Dialog, Dialog.Show(Owner), Directory);
  except
    Log('Native folder dialog unavailable: ' + GetExceptionMessage);
  end;
end;

procedure NativeDirectoryBrowseClick(Sender: TObject);
var
  Directory, Fallback: String;
  Status: Integer;
begin
  Directory := WizardForm.DirEdit.Text;
  Status := BrowseNativeFolder(WizardForm.Handle, Directory);
  if Status = NativeFolderSelected then
    WizardForm.DirEdit.Text := Directory
  else if Status = NativeFolderUnavailable then begin
    { Only an unavailable Windows dialog uses the legacy fallback. A normal
      cancel never opens another dialog or changes the current target. }
    Fallback := ExistingBrowseFolder(WizardForm.DirEdit.Text);
    if BrowseForFolder(CustomMessage('SelectInstallFolder'), Fallback, False) then
      WizardForm.DirEdit.Text := InstallFolderBelow(Fallback);
  end;
end;

<event('InitializeWizard')>
procedure InitializeNativeFolderPicker;
begin
  WizardForm.DirBrowseButton.OnClick := @NativeDirectoryBrowseClick;
end;

{ Called only by a headless verification program. It deliberately never calls
  Show: options, shell path marshalling and vtable slots are exercised without UI. }
#ifdef NativeFolderPickerProbe
function ProbeNativeFolderPicker(const Folder: String): String;
var
  Dialog: INativeFileDialog;
  Item: INativeShellItem;
  Options: DWORD;
  BeforeCancel, AfterCancel: String;
begin
  Dialog := CreateNativeFolderDialog(Folder);
  OleCheck(Dialog.GetOptions(Options));
  if (Options and (FOS_PICKFOLDERS or FOS_FORCEFILESYSTEM or FOS_PATHMUSTEXIST)) <>
    (FOS_PICKFOLDERS or FOS_FORCEFILESYSTEM or FOS_PATHMUSTEXIST) then
    RaiseException('Folder-picking option was not applied.');
  OleCheck(Dialog.GetFolder(Item));
  Result := NativeShellItemPath(Item);
  if CompareText(RemoveBackslashUnlessRoot(Result), RemoveBackslashUnlessRoot(Folder)) <> 0 then
    RaiseException('Native folder round-trip mismatch.');
  OleCheck(Dialog.AddPlace(NativeShellItem(Folder), FDAP_TOP));
  if InstallFolderBelow('C:\') <> 'C:\ScreenshotTool' then
    RaiseException('Root folder append failed.');
  if InstallFolderBelow('C:\Example') <> 'C:\Example\ScreenshotTool' then
    RaiseException('Application folder append failed.');
  if InstallFolderBelow('C:\Example\ScreenshotTool') <> 'C:\Example\ScreenshotTool' then
    RaiseException('Application folder was appended twice.');
  BeforeCancel := AddBackslash(Folder) + 'KeepThisTarget';
  AfterCancel := BeforeCancel;
  if CompleteNativeFolderDialog(Dialog, CancelledHResult, AfterCancel) <> NativeFolderCancelled then
    RaiseException('Cancel was not recognized.');
  if AfterCancel <> BeforeCancel then
    RaiseException('Cancel changed the existing installation target.');
end;
#endif
