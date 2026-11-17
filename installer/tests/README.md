# 原生文件夹选择器无界面验证

`NativeFolderPickerProbe.iss` 引用产品使用的同一个 `NativeFolderPicker.iss`。它在 `InitializeSetup` 中创建 Windows 原生 COM 对话框对象，验证选文件夹选项、Unicode 路径的 SetFolder/GetFolder/GetDisplayName、CoTaskMem 字符串释放、AddPlace、根目录追加应用名、避免重复追加和取消保持目标不变。

探针从不调用 `Show`，并在创建向导窗体之前退出，不安装、卸载、关闭应用或操作桌面。进程退出码 **1** 是 `InitializeSetup=False` 的预期结果；同时必须检查 UTF-8 结果文件以 `PASS:` 开头。

在仓库根目录执行：

```powershell
$probeOutput = Join-Path (Get-Location) '.tools/folder-picker-probe'
.\.tools\inno\ISCC.exe "/DProbeOutputDir=$probeOutput" installer/tests/NativeFolderPickerProbe.iss
$fixture = Join-Path $probeOutput '路径 空格 é'
New-Item -ItemType Directory -Path $fixture -Force | Out-Null
$result = Join-Path $probeOutput 'result.txt'
$process = Start-Process (Join-Path $probeOutput 'NativeFolderPickerProbe.exe') -WindowStyle Hidden -Wait -PassThru -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/SP-', ('/folder="' + $fixture + '"'), ('/result="' + $result + '"'))
if ($process.ExitCode -ne 1 -or [IO.File]::ReadAllText($result) -notlike 'PASS:*') { throw '原生文件夹选择器探针失败' }
```

交互界面外观、键盘导航和取消操作仍需人工验证；无界面探针不将这些项目标为通过。

编译检查必须使用独立输出目录。Inno Setup 即使指定 `/O-`，也可能删除目标输出目录中同名的已有 EXE；不要用交付安装包所在目录作语法检查输出目录。

实现依据：[Inno 6.7.4 自带的 IUnknown 示例](https://github.com/jrsoftware/issrc/blob/is-6_7_4/Examples/CodeAutomation2.iss)、[微软 Common Item Dialog](https://learn.microsoft.com/en-us/windows/win32/shell/common-file-dialog)、[FOS_PICKFOLDERS](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-_fileopendialogoptions)、[AddPlace](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifiledialog-addplace)。
