# Windows 验收记录

记录日期：2026-11-16（持续追加）。`未执行` 表示没有该环境或尚未操作；构建成功和自动化行为测试不能替代真实桌面验收。发现问题时记录实际表现、日志、复现步骤和截图，不将目标行为当成测试结果。

## 1.0.4 当前发布

新增标注控制点：使用“选择”工具点击已有标注，箭头显示两个端点，矩形和椭圆显示八个边角控制点；拖动主体仍整体移动。新截图和贴图再次编辑共用此操作，一次拖动只提交一步撤销，控制点不进入图片输出。

- 新增 16 项测试通过；非原生回归 77/77 通过，Release 构建 0 警告、0 错误。父代理随后独立运行完整 Release 套件，**82/82 通过、0 失败、0 跳过**，证据位于 `.superpowers/sdd/2026-11-16-annotation-handles/`。`red.log`、`scale-red.log`、`noop-red.log` 保留失败先行证据，`release-all.log` 为最终完整回归。
- 回归覆盖另一箭头端点固定、边角跨越对边归一化、一次拖动一次撤销、无变化不增加历史、非零选区坐标、真实 Viewbox 缩小后的显示和命中、松手最终坐标、贴图草稿隔离、主体移动与连续绘图、导出无控制点。独立代码审查通过。
- 已查看真实离屏 EditorView 效果图 `handles-preview.png`；未接管用户鼠标键盘。完整手动绘图交互及干净系统安装验收仍未执行。
- 直接运行：`artifacts/publish/1.0.4/win-x64/ScreenshotTool.exe`，文件版本 `1.0.4.0`；SHA256 `74631d46d3be41eb11af023669d0d2b473c76cbec6e38aa027b1034f6df1f431`。
- 免安装 ZIP：`artifacts/portable/ScreenshotTool-1.0.4-win-x64.zip`，`73,793,662` 字节；SHA256 `ba22bbf928d76b7ba0111a4b104318957ef9bffd14f3e02cc88c31e09595a3df`。472 个源文件加中文说明，共 473 个 ZIP 条目逐项校验通过。
- 安装包：`artifacts/installer/1.0.4/ScreenshotTool-Setup-x64.exe`，`53,556,102` 字节；SHA256 `6bd91627ab91bfd6d3d63c255d8c6093816495ad2caad2adbf2bad1a6448f9d2`。自包含发布、Inno 编译及父代理核对清单哈希均通过。
- 1,445 个旧发行文件的逐文件 SHA256 全部保持一致，见 `previous-release-preservation.json`；未启动新版、关闭用户程序或执行安装。

## 1.0.3 历史发布

本次修复应用自身窗口淡出的残影进入截图：隐藏自身窗口时关闭其 DWM 过渡，在取得屏幕快照之前等待桌面合成完成，移除原先固定的 100 毫秒等待；保留隐藏/准备失败时的会话和贴图恢复。

- 已执行 `scripts/package.ps1 -Configuration Release -Version 1.0.3` 和 `scripts/package-portable.ps1 -Version 1.0.3`，均退出码 0；使用新的版本目录，未关闭用户程序。
- 直接运行：`artifacts/publish/1.0.3/win-x64/ScreenshotTool.exe`；SHA256 `d14fc6f3c0832ddecdc22aecb5cb5e6b0c1a452c8b5b15bdaa0ba775cd44be39`。复制到其他电脑时须保留整个发布目录。
- 免安装 ZIP：`artifacts/portable/ScreenshotTool-1.0.3-win-x64.zip`，`73,791,566` 字节；SHA256 `00e4fb31647ae6e37dfba0412e0391881668a97d131a3f2ff0bc4c9ae1dedd66`。472 个源文件和中文说明共 473 个条目的 SHA256 全部核对通过，条目使用 `/` 和 UTF-8 文件名。
- 安装包：`artifacts/installer/1.0.3/ScreenshotTool-Setup-x64.exe`，`53,555,837` 字节；SHA256 `2912b6494d0d9c1e775b95cd9db33a7894e8ce4f6a04f6984dd749db15dfcca8`。同目录保存 `dotnet-publish.log`、`inno-compile.log` 和 `build-manifest.json`。
- 打包前后核对 964 个既有发布文件，逐文件 SHA256 全部相同；见 `artifacts/installer/1.0.3/previous-release-preservation.json`。
- 原生回归先复现失败：隐藏红色窗口后，屏幕捕获采样仍为 BGR `[7, 0, 248]`，预期为后方蓝色窗口的 `[255, 0, 0]`。修复后同一原生像素断言及复用同一 HWND 再次隐藏的用例通过；同时验证准备动作先于捕获且每会话只执行一次，以及隐藏/准备失败的恢复顺序。
- Debug 和新编译的 Release 套件均 **66/66 通过、0 失败、0 跳过**。证据：`.superpowers/sdd/2026-11-13-screenshot-tool/capture-visibility-red.log`、`capture-visibility-green.log`、`capture-visibility-release.log`。
- 用户当前会话中的完整截图交互、不同桌面主题/硬件组合尚未人工验收；本轮没有电脑控制、输入操作、安装、卸载或关闭用户应用。以下保留 1.0.2 的历史证据。

## 已执行的环境与证据

- 构建主机：Windows x64，OS 版本 `10.0.26200`；这是当前开发环境，不是未安装 .NET 的干净系统。
- 工具链：工作区便携 .NET SDK `10.0.404`，MSBuild `18.9.15+2ab285486`，.NET / Windows Desktop Runtime `10.0.15`。
- 安装器编译器：Inno Setup `6.7.4`，已通过无输出探针脚本实际编译确认；官方安装程序经 SHA256 和有效的 Pyrsys B.V. Authenticode 签名验证。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap.ps1`：已执行，退出码 0；重复使用本地工具，没有全局安装。
- PowerShell 版本：`5.1.26100.9711`；当前执行令牌未提升权限。通过 Inno `/O-` 对实际安装器脚本和中文语言文件完成无输出编译，退出码 0；这只验证编译，不代表安装器运行已验收。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Configuration Release -Version 1.0.2`：已执行，退出码 0。使用独立 `artifacts/build/win-x64/1.0.2` 编译/中间/还原目录及 `artifacts/publish/1.0.2/win-x64` 发布目录；自包含发布及 Inno 编译成功，包含 .NET 与 Windows Desktop Runtime `10.0.15`。构建未关闭任何用户程序。
- 当前安装包：`artifacts/installer/1.0.2/ScreenshotTool-Setup-x64.exe`，`53,554,795` 字节；SHA256 `445ef1fac22caebed0792ddff877d6612699293614bce6590a8961d2e07f0d9a`。构建记录时间 `2026-11-13T09:20:26Z`；同目录保存 `build-manifest.json`、`dotnet-publish.log` 和 `inno-compile.log`。
- 免安装 EXE：`artifacts/publish/1.0.2/win-x64/ScreenshotTool.exe`；SHA256 `35016fd99012b43ff0deeea557326af60cc5c7bfb3c51b7767f5b90cdc8d3c86`。移动到其他电脑时应携带完整发布目录。
- `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-portable.ps1 -Version 1.0.2`：已执行，退出码 0；生成 `artifacts/portable/ScreenshotTool-1.0.2-win-x64.zip`，`73,791,218` 字节，SHA256 `cee7cf989ed53906370216fe11060704cfef476b25fbb8916e1cc469db045fb9`。逐项核对 472 个源文件与中文免安装说明，共 473 个 ZIP 条目的 SHA256；核对条目使用 `/` 路径，默认读取器可正确读取 UTF-8 中文说明文件名。校验值及验证清单保存在同目录的 `.sha256`、`.manifest.json`。
- 原先 `artifacts/publish/win-x64` 的 472 个文件在本次打包前后数量与逐文件 SHA256 完全相同；证据为 `artifacts/installer/1.0.2/previous-release-preservation.json`。新版发布和安装器默认均按版本保存，旧目录未被清理或替换。
- 旧版 1.0.1 安装包 `artifacts/installer/1.0.1/ScreenshotTool-Setup-x64.exe` 保留，SHA256 `79ce3be02e6912bcee75c76e1067e136847eb897ca02e6fc782f1a3231f90cb8`。
- 旧版 1.0.0 下载路径 `artifacts/installer/ScreenshotTool-Setup-x64.exe` 已保留，`53,553,521` 字节，SHA256 `75c56e5501a74918c49d90d1195eea8edf732c879cab4769b314d6f43ea59aad`。新版使用独立子目录。
- 当前发布目录递归包含 472 个文件；已核对 `coreclr.dll`、`hostfxr.dll`、`PresentationFramework.dll` 和带 `includedFrameworks` 的 runtimeconfig 文件。实际无外部运行时启动及安装验证待执行。
- 1.0.2 托盘回调调整后的 Debug 自动化套件 **61/61 通过、0 失败、0 跳过**；证据为 `.superpowers/sdd/2026-11-13-screenshot-tool/tray-ui-green.log`。套件覆盖选区原点保留、125%/150%/200% 显示映射、负原点跨屏边界、文件像素输出、草稿导出及取消等行为。此次小范围回调接线调整没有增加模拟实现的测试，也未执行真实托盘点击手势验收。
- 1.0.1 发布期间出现 NU1900 警告：NuGet 包漏洞元数据的 SSL 连接失败；本地依赖还原和发布编译仍成功。
- 原生文件夹选择器使用产品同一份 Pascal COM 声明编译及运行无界面探针，通过 Unicode 路径 `路径 空格 é` 的 SetFolder/GetFolder/GetDisplayName、CoTaskMem 字符串复制/释放、文件夹选项、AddPlace、根目录追加、避免重复追加、取消保持目标。探针从不调用 Show，在创建任何向导窗体之前退出；预期进程退出码为 1，UTF-8 结果文件为 `PASS:`。证据在 `.tools/folder-picker-probe/compile.log` 和 `result.txt`，复现见 [探针说明](../../installer/tests/README.md)。
- 新版原生选择器的真实外观、导航和交互取消仍待用户验收；真实截图窗口 Esc 取消/返回托盘，以及左键单击托盘打开设置、双击不触发截图，均未完成交互验证。按用户要求，后续未继续电脑控制、安装、卸载或关闭当前用户的应用。

工具下载与校验：

| 文件 | 官方来源 | 校验值 |
| --- | --- | --- |
| .NET SDK 10.0.404 win-x64 ZIP | [微软发布元数据](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json) | SHA512 `330ddd2665b43cd2e428216a0d8ea1ac7bb19b5434fdc530e62010dc3b52823a620bf95489d628e1ed6f5122401793ee4887cca4a019122361a1231497f632ee` |
| Inno Setup 6.7.4 安装程序 | [官方发布](https://github.com/jrsoftware/issrc/releases/tag/is-6_7_4) | SHA256 `a549929319f9120716677c685535ca4671e4b937b5fdc87e2585f32d8eb30e8c` |
| 提取后的 ISCC.exe | 上述已验证的官方安装程序 | SHA256 `f02fa0e5f237b47d7ed1edae9b991866ec94fe02af61eb6a73e94f92fe8e0604` |

## 干净系统安装与分发

在每台虚拟机或真实电脑记录系统版本、系统架构、账户权限、已有 .NET 列表、网络状态、安装器版本及 SHA256。

| 场景 | Windows 10 22H2 x64 | Windows 11 x64 | 操作与预期 |
| --- | --- | --- | --- |
| 离线普通用户安装 | 未执行 | 未执行 | 干净系统无 .NET，断网，以标准用户运行安装器；无提权提示，安装完成 |
| 启动与快捷方式 | 未执行 | 未执行 | 开始菜单和可选桌面快捷方式可启动；托盘存在；系统未安装 .NET 仍可使用 |
| 默认与自定义热键 | 未执行 | 未执行 | Ctrl+Alt+S 可截图；修改即生效；重启保留设置；占用候选键时旧热键可用 |
| 单实例与启动项 | 未执行 | 未执行 | 再次启动显示已有设置且只有一份进程/托盘；开机启动默认关闭、开关有效 |
| 运行中升级 | 未执行 | 未执行 | 设置非默认热键并保存图片、保留贴图后安装较新版本；运行实例退出、程序成功替换、设置和导出图片保留、重启不恢复贴图 |
| 卸载 | 未执行 | 未执行 | 应用和快捷方式移除，安装路径对应的本用户启动项移除，设置和主动导出的图片保留；无提权 |
| 原生目录选择 | 未执行 | 未执行 | “浏览”显示系统原生地址栏及常用位置，桌面/文档可达；选根目录追加应用名，选同名目录不重复追加，取消不改变原目标 |

## 操作结果逐项验收

先向剪贴板复制一个可识别的对照内容，再执行每一项。记录贴图数量、剪贴板内容、输出文件和编辑状态。每项目前均为 **未执行**。

| 操作 | 预期结果 |
| --- | --- |
| 新截图“复制并贴图” | 编辑结束；复制图像；仅新增一张贴图 |
| 新截图“仅复制” | 编辑结束；复制图像；贴图数量不变 |
| 新截图“仅贴图” | 编辑结束；仅新增一张贴图；剪贴板保持对照内容 |
| 新截图保存成功 | 仅生成 PNG/JPG 并结束编辑；剪贴板和贴图不变 |
| 保存对话框取消/写入失败 | 继续编辑；没有额外输出；可选新路径重试 |
| 贴图编辑确认 | 更新同一贴图及其可独立选择的标注；不新增贴图、不自动复制 |
| 贴图编辑取消 | 原图和原标注保持不变；不自动复制 |
| 贴图编辑中保存草稿 | 导出草稿，继续编辑；原贴图尚不改变；取消编辑后导出文件仍保留 |
| 贴图窗口复制/保存 | 显式执行对应输出；不进入编辑、不新增贴图 |
| 取消新截图 | 剪贴板、文件和贴图数量不变 |

## 桌面与错误路径

| 场景 | 步骤与检查 | 本次结果 |
| --- | --- | --- |
| 框选/窗口选取/全屏 | 检查冻结画面、可见顶层窗口边界、鼠标所在显示器全屏；移动和调整选区，核对像素尺寸 | 未执行 |
| 混合 DPI | 125%/150%/200% 屏幕组合，截图含像素标尺；核对选区、标注及导出尺寸/边缘 | 未执行 |
| 负坐标和跨屏 | 将副屏放主屏左侧或上方；双向跨屏拖动，移动及调整选区；确认不偏移、不拉伸标注 | 未执行 |
| 六种标注 | 画笔、矩形、椭圆、箭头、中文文字、马赛克；改变颜色/线宽/字号；选择、移动、删除；Undo/Redo；检查真实导出像素 | 未执行 |
| 标注控制点 | 新截图及贴图再次编辑中选择箭头，拖动两端改变长度/角度；矩形/椭圆拖动八个边角改变宽高；主体仍可平移；缩小画面仍易命中；一次撤销恢复一次拖动，导出不含控制点 | 自动化交互路由/几何/像素回归通过，离屏界面图已检查；完整手动鼠标交互未执行 |
| 中文输入与 Enter | 开启中文 IME，在文字框确认候选/换行；不完成整个截图；离开文字输入后 Enter 可完成 | 未执行 |
| 多贴图与再次编辑 | 多张置顶、拖动、缩放、透明度、选择原标注再次修改；复制/保存仍保持源像素尺寸与不透明内容 | 未执行 |
| 临时隐藏的恢复 | 有多张贴图时开始截图；分别完成、取消和模拟捕获失败；贴图按先前可见状态恢复 | 未执行 |
| 设置窗口淡出残影 | 打开设置后触发截图，检查冻结画面中无设置窗口及其淡出残影；取消和完成后检查贴图恢复，重新打开设置后再次截图检查无残影 | 原生像素回归通过；完整用户交互未执行 |
| 剪贴板占用 | 另一进程暂时占用剪贴板，确认失败后保持编辑，解除占用并重试；成功复制且只新增一张贴图 | 未执行 |
| 不可写目录 | 保存到无权限或已断开的目录；提示可理解，可继续编辑并选其他目录；成功后才记忆新目录 | 未执行 |
| 重复确认与热键 | 快速重复热键/Enter；仅一个编辑会话和一次输出；不生成重复贴图 | 未执行 |
| 截图失败 | 注入或制造可重现的捕获失败；恢复托盘和贴图；之后可再次截图 | 未执行 |
| 退出和资源 | 创建并关闭多张大图、取消会话，观察内存/句柄趋于稳定；退出无逐图保存提示、下次无图像恢复、剪贴板不被清空 | 未执行 |

## 复现命令与填写格式

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/bootstrap.ps1
.\.tools\dotnet\dotnet.exe test --solution ScreenshotTool.slnx -c Release
.\.tools\dotnet\dotnet.exe build ScreenshotTool.slnx -c Release
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package.ps1 -Configuration Release -Version 1.0.4
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/package-portable.ps1 -Version 1.0.4
Get-FileHash artifacts/installer/1.0.4/ScreenshotTool-Setup-x64.exe -Algorithm SHA256
Get-FileHash artifacts/portable/ScreenshotTool-1.0.4-win-x64.zip -Algorithm SHA256
```

每次实测追加：日期、测试人、系统/屏幕与缩放、安装器版本/哈希、执行命令及退出码、已通过项、失败项/复现步骤、未执行项原因。自动生成的 `build-manifest.json` 与 `inno-compile.log` 随安装器保留。
