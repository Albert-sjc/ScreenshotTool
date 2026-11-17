# ScreenshotTool

Windows 截图、标注和桌面置顶贴图工具。界面使用简体中文，常驻托盘，默认按 **Ctrl+Alt+S** 开始截图。支持框选、窗口选取、鼠标所在显示器全屏、六种标注、PNG/JPG 导出，以及本次运行期间保留原标注的贴图编辑。

首版目标系统为 Windows 10 22H2 x64 和 Windows 11 x64。安装包包含 .NET 运行时，仅为当前用户安装。真实系统和硬件场景的验证状态见 [Windows 验收记录](docs/testing/windows-acceptance.md)。

## 使用

运行 `installer/ScreenshotTool-Setup-x64.exe` 完成安装。左键单击托盘图标打开设置界面，双击不会开始截图；右键菜单提供截图、设置和退出入口，也可按 Ctrl+Alt+S 截图。再次启动会唤起已有实例。切换到新版前，请自行从旧实例的托盘菜单退出，再运行新版。

1. 按 Ctrl+Alt+S 或使用托盘入口，拖动框选，或选择一个可见顶层窗口；全屏操作截取鼠标所在显示器。
2. 使用画笔、矩形、椭圆、箭头、文字、马赛克添加标注。可选择、移动、删除标注，并撤销和重做。使用“选择”工具选中箭头后，拖动两端控制点调整长度和角度；矩形、椭圆可拖动八个边角控制点调整宽高。一次拖动可用一次撤销恢复，贴图再次编辑同样适用。
3. Enter 按设置的完成动作输出，默认“复制并贴图”；Esc 取消。文字输入框中的 Enter 用于换行。
4. Ctrl+S 导出普通 PNG/JPG。新截图保存成功即结束，仅保存；取消或失败可继续编辑。
5. 贴图可以置顶、移动、缩放、调整透明度、复制、保存和再次编辑。贴图编辑确认更新原贴图，取消保留原图；编辑中保存只导出草稿。

设置可以修改全局快捷键、完成动作、默认图片格式和开机启动。开机启动默认关闭。快捷键冲突会保留旧设置，也可以继续从托盘截图。

截图和独立标注仅保留在当前运行期间。关闭贴图或退出后不恢复；主动导出的图片文件会保留。缩放和透明度仅影响贴图显示，导出保持原始像素尺寸。应用仅保存当前用户设置，不维护截图历史。

## 本地构建

需要 Windows x64、Windows PowerShell 5.1 或 PowerShell 7。首次引导工具链及还原 NuGet 包需要联网；分发给最终用户的安装包运行时不需要联网或另装 .NET。

在仓库根目录运行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap.ps1
.\.tools\dotnet\dotnet.exe restore ScreenshotTool.slnx
.\.tools\dotnet\dotnet.exe test --solution ScreenshotTool.slnx -c Release
.\.tools\dotnet\dotnet.exe build ScreenshotTool.slnx -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Configuration Release -Version 1.0.4
```

`bootstrap.ps1` 将微软官方 .NET SDK 10.0.404 ZIP 解压至 `.tools/dotnet`，将官方 Inno Setup 6.7.4 以 `/PORTABLE=1` 模式提取至 `.tools/inno`。它核对发布文件的哈希，并验证 Inno Setup 下载文件的 Pyrsys B.V. 数字签名；不安装全局 SDK、修改 PATH 或申请管理员权限。固定版本、下载地址与校验值保存在脚本和 `.tools/toolchain.json` 中。已有 SDK 时可使用 `-SkipInno` 仅准备 SDK。

也可以使用已安装的 .NET 10 SDK 与 Inno Setup 编译器。打包脚本优先使用 `.tools` 中的版本，支持 `-DotnetPath <dotnet.exe>`、`-IsccPath <ISCC.exe>` 或 `ISCC_PATH` 环境变量。仅有 .NET Runtime 时会给出缺少 SDK 的错误。

`package.ps1` 以 `win-x64 --self-contained true` 发布，不裁剪 WPF 运行库，然后编译离线安装器。编译与中间文件使用独立的 `artifacts/build` 路径，不替换正在运行的源码构建目录。它核对应用和运行库文件，检查每个工具的退出码；成功时产生：

- `artifacts/publish/<Version>/win-x64/`：完整可运行的发布目录；直接运行其中的 `ScreenshotTool.exe`。
- `installer/ScreenshotTool-Setup-x64.exe` 是默认生成的离线安装包，可复制到其他电脑安装。
- 同目录的 `ScreenshotTool-Setup-x64.exe.sha256`：安装器校验值。
- 同目录的 `build-manifest.json`：本次版本、工具版本、时间和安装器 SHA256。
- 同目录的 `inno-compile.log`：安装器编译记录。
- 同目录的 `dotnet-publish.log`：应用发布构建记录。

脚本默认版本为 `1.0.4`。发布时传入实际的三段版本号，可带预发行后缀，例如 `1.0.4-rc.1`。应用的发布目录按版本保存。每次编译成功后，新的安装包会替换 `installer/` 中的同名文件。可用 `-InstallerOutputDirectory` 将安装包和构建记录输出到其他目录。如果目标发布目录中的程序仍在运行，脚本会提示选择新版本。安装器产品标识在所有版本间保持一致。

## 安装、升级与卸载

默认安装目录为 `%LOCALAPPDATA%\Programs\ScreenshotTool`，开始菜单和可选桌面快捷方式仅属于当前用户。安装器声明 `PrivilegesRequired=lowest`，不修改系统范围的配置。

1.0.1 的“浏览”按钮使用 Windows 原生文件夹选择界面，保留系统导航栏和地址栏，并添加桌面、文档入口。选择上级目录后会追加 `ScreenshotTool` 子目录；已选中同名目录时不重复追加，取消保持原目标。只有系统原生对话框不可用时才回退到旧目录树。实现直接调用系统 COM 接口，不需要额外辅助程序或运行时。无界面验证方法见 [安装器探针说明](installer/tests/README.md)；交互外观与取消仍等待用户验收。

升级和卸载会向运行中的应用发送退出请求，安装时再由 Windows Restart Manager 检查占用文件。退出将关闭本次运行的贴图；升级后不恢复贴图。设置位于应用安装目录之外，升级和卸载均保留设置。用户主动导出的图片由用户选择目录，安装器不会删除这些图片。卸载只移除指向当前安装路径的本用户开机启动项；重新安装后可按保留的设置恢复。

安装器尚未进行产品代码签名。请使用生成的 SHA256 核对传输后的文件；这不替代发行者的数字签名。

## 需求与实现

权威需求在 [.scratch/screenshot-tool/spec.md](.scratch/screenshot-tool/spec.md)，实施计划在 [docs/superpowers/plans/2026-11-13-screenshot-tool.md](docs/superpowers/plans/2026-11-13-screenshot-tool.md)。测试使用 xUnit.net v3 与 Microsoft.Testing.Platform。

工具链参考：[微软手动安装说明](https://learn.microsoft.com/en-us/dotnet/core/install/windows#manual-install)、[自包含发布说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/#publish-self-contained)、[Inno Setup 官方下载](https://jrsoftware.org/isdl.php)、[官方便携模式实现](https://github.com/jrsoftware/issrc/blob/is-6_7_4/isportable.iss)。
