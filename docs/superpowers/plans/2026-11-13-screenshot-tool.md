# ScreenshotTool Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现已确认的 Windows 截图、标注、可编辑贴图和当前用户离线安装包。

**Architecture:** 一个 WPF 应用项目和一个测试项目。以截图工作流作为主要测试入口，图像文档保留原图及可编辑标注，Windows 原生能力与输出操作放在少量边界后；不引入后台服务、数据库或网络依赖。

**Tech Stack:** C#、WPF、.NET 10 LTS、xUnit.net v3、Inno Setup；Windows x64 自包含发布。

**Spec:** `.scratch/screenshot-tool/spec.md`；设计说明：`docs/superpowers/specs/2026-11-13-screenshot-tool-design.md`。

## Global Constraints

- 使用 C# + WPF + .NET 10 LTS；Windows 桌面集成使用原生能力。
- 以 Windows 10 22H2 x64 与 Windows 11 x64 作为首版兼容性验收目标。
- 初始截图快捷键为 Ctrl+Alt+S；被占用时提示改键，托盘截图入口仍可用。修改失败保留旧快捷键。
- 开机启动默认关闭；默认完成动作为复制并贴图；默认图片格式为 PNG。
- 一次只允许一个截图或贴图编辑会话，重复截图快捷键不创建并行会话。
- Windows x64 自包含发布，使用 Inno Setup 制作当前用户离线安装 EXE；不请求管理员权限。
- 取消贴图编辑不删除之前主动导出的草稿文件。
- 关闭贴图或正常退出不逐张提示保存；无历史和重启恢复。退出不主动清空系统剪贴板。
- 不加入规格 Out of Scope 中的功能。各任务只建立其交付物需要的文件与抽象。

## Review Focus

1. 混合 DPI、负坐标与显示器接缝：用户选中的像素应与导出一致，交给 Task 2 的接缝样本与真实多屏验证。
2. 中文输入及文字框中的 Enter：只能换行，不能提前完成截图，交给 Task 3 的输入路由测试与中文 IME 验证。
3. 剪贴板忙时重复确认：保留编辑，重试后只复制一次并创建一张贴图，交给 Task 4 的失败与重复输入测试。
4. 贴图草稿导出后再取消编辑：导出文件存在而原贴图未变，交给 Task 5 的工作流测试。
5. 应用正在运行时覆盖升级：正确退出后替换应用，保留用户设置及导出图片，交给 Task 7 的安装验收。

## 执行前提与仓库状态

- 当前仓库没有产品代码，也没有 Git 仓库；本计划中的源文件均为待创建路径，不是现有文件。
- 2026-11-13 检查：`dotnet` 命令存在，但 `dotnet --list-sdks` 未列出 SDK。Inno Setup 编译器未在 PATH 和已检查的默认安装位置找到。
- 开始 Task 1 时先具备 .NET 10 SDK，记录实际 SDK 精确版本到 `global.json`；Task 7 打包前具备 Inno Setup。当前计划编写不安装它们。
- 使用当前稳定的 xUnit.net v3 模板，启用 Microsoft.Testing.Platform；测试目标改为 `net10.0-windows` 并启用 WPF，以便测试实际 BitmapSource 和渲染结果。模板与运行器以 [xUnit 官方说明](https://xunit.net/docs/getting-started/v3/code-coverage-with-mtp) 为准，生成后固定依赖版本。
- 测试统一命令：`dotnet test --project tests/ScreenshotTool.Tests/ScreenshotTool.Tests.csproj`。每个新增场景先运行得到目标功能的失败，再做最小实现并运行通过。
- Git 提交仅在执行时已有可写的 Git 仓库时按任务进行；本计划不要求初始化仓库。

## 文件与职责

| 待创建位置 | 职责 |
| --- | --- |
| `ScreenshotTool.slnx`、`global.json` | 解决方案、实际 SDK 与测试运行器配置 |
| `src/ScreenshotTool/ScreenshotTool.csproj`、`App.xaml`、`App.xaml.cs` | WPF 入口、依赖装配、生命周期 |
| `src/ScreenshotTool/Desktop/` | 原生快捷键、捕获、显示器、剪贴板与托盘 |
| `src/ScreenshotTool/Capture/` | 屏幕坐标、截图会话、选区遮罩 |
| `src/ScreenshotTool/Editing/` | 图像文档、标注、渲染、编辑控件 |
| `src/ScreenshotTool/Workflow/` | 完成与保存的状态和副作用 |
| `src/ScreenshotTool/Pins/` | 当前运行期间的贴图及可取消编辑 |
| `src/ScreenshotTool/Settings/` | 用户设置、快捷键录入和开机启动 |
| `tests/ScreenshotTool.Tests/` | 工作流、像素结果、设置和输入行为测试 |
| `installer/ScreenshotTool.iss`、`scripts/package.ps1` | 当前用户安装及发布打包 |
| `docs/testing/windows-acceptance.md` | 真实系统验收步骤与结果记录 |

## Task 1: 热键到框选复制的首个完整流程

**Files:** Create `ScreenshotTool.slnx`, `global.json`, `src/ScreenshotTool/ScreenshotTool.csproj`, `src/ScreenshotTool/App.xaml`, `src/ScreenshotTool/App.xaml.cs`, `src/ScreenshotTool/Desktop/HotkeyService.cs`, `src/ScreenshotTool/Desktop/ScreenCaptureService.cs`, `src/ScreenshotTool/Desktop/ClipboardService.cs`, `src/ScreenshotTool/Desktop/TrayController.cs`, `src/ScreenshotTool/Capture/PixelGeometry.cs`, `src/ScreenshotTool/Capture/CaptureCoordinator.cs`, `src/ScreenshotTool/Capture/CaptureOverlay.xaml`, `src/ScreenshotTool/Capture/CaptureOverlay.xaml.cs`, `tests/ScreenshotTool.Tests/ScreenshotTool.Tests.csproj`, `tests/ScreenshotTool.Tests/CaptureCoordinatorTests.cs`, `tests/ScreenshotTool.Tests/Sta.cs`.

**Interfaces:**
- Produces `PixelPoint(int X, int Y)` and `PixelRect(int X, int Y, int Width, int Height)` as immutable value types, plus `ScreenSnapshot(BitmapSource Pixels, PixelPoint Origin)`.
- Produces `IHotkeyRegistrar.TryRegister(Hotkey hotkey): RegistrationResult`; define `Hotkey(int Modifiers, int VirtualKey)` and `RegistrationResult(bool Success, string? Error)` in `Desktop/HotkeyService.cs`. Native Ctrl+Alt+S is modifiers `3`, virtual key `83`.
- Produces `IScreenCapture.Capture(): ScreenSnapshot` and `IClipboard.Copy(BitmapSource image): OutputResult`; `OutputResult(bool Success, string? Error)` lives with the output boundary until Task 4 consolidates it.
- Produces `CaptureCoordinator.Start(): bool`, `ConfirmSelection(PixelRect selection): OutputResult`, `Cancel(): void`, `IsEditing: bool`. Start returns false while a session is active. These are the first behavior-testing entry points.
- Test helper `Sta.Run(Action action): void` runs WPF-dependent tests on an STA thread, propagates exceptions and joins it; test images are frozen before crossing threads.

- [ ] Prepare the SDK, solution and test project, using `net10.0-windows` for both projects. Enable WPF; enable tray integration through the Windows Forms framework reference without another UI framework package.
- [ ] Write `StartWhileEditing_DoesNotCaptureTwice`: call Start twice using a counting capture fake; assertions `Assert.True(first); Assert.False(second); Assert.Equal(1, captures);`.
- [ ] Write `Cancel_DoesNotCopy` and `ClipboardFailure_KeepsEditing`: cancellation leaves copy count `0`; failed copy yields `Assert.True(coordinator.IsEditing)`.
- [ ] Run the test command and confirm the new behavior is failing, rather than accepting restore/toolchain errors as evidence.
- [ ] Implement tray Start/Exit, hotkey registration, one SDR desktop snapshot, drag selection, Enter copy and Esc cancel. Capture only on demand; dispose native handles after transferring pixels. Do not expose unfinished annotation or pin actions yet.
- [ ] Run the tests and `dotnet build ScreenshotTool.slnx -c Release`; manually verify background hotkey → rectangle selection → paste into Paint, and cancel → clipboard unchanged.

## Task 2: 自动窗口选取、全屏与跨屏像素对齐

**Files:** Create `src/ScreenshotTool/Desktop/MonitorService.cs`, `src/ScreenshotTool/Desktop/WindowPicker.cs`, `src/ScreenshotTool/Capture/SelectionModel.cs`, `tests/ScreenshotTool.Tests/SelectionModelTests.cs`; modify `Desktop/ScreenCaptureService.cs`, `Capture/PixelGeometry.cs`, `Capture/CaptureOverlay.xaml`, `Capture/CaptureOverlay.xaml.cs`.

**Interfaces:**
- Consumes `ScreenSnapshot`, `PixelPoint`, `PixelRect` from Task 1.
- Produces `MonitorInfo(PixelRect Bounds, double ScaleX, double ScaleY)` and `MonitorService.At(PixelPoint pointer): MonitorInfo`.
- Produces `WindowPicker.TryPick(PixelPoint point): PixelRect?` for visible top-level windows; consumes physical screen coordinates.
- Produces `SelectionModel.FromDrag(PixelPoint start, PixelPoint end): PixelRect` and `ToSnapshot(PixelRect global, PixelPoint origin): PixelRect`; WPF display conversion stays at the overlay boundary.

- [ ] Write `NegativeOrigin_MapsToSnapshotPixels` with global `(-1800,100,400,200)` and origin `(-1920,0)`; assert `Assert.Equal(new PixelRect(120,100,400,200), result)`.
- [ ] Write `ReverseDrag_NormalizesBounds` with start `(300,200)`, end `(100,50)`; assert rectangle `(100,50,200,150)`. Add boundary samples with scales `1.25`, `1.5`, `2.0` and a selection crossing a monitor seam; assert the physical rectangle dimensions remain unchanged by display conversion.
- [ ] Run the tests and observe the missing coordinate behavior.
- [ ] Implement window hover/click, drag override, handles to resize/move selection, pixel-size label, current-monitor fullscreen, and virtual desktop capture. Initialize per-monitor DPI awareness before creating windows.
- [ ] Run tests/build; on real mixed-DPI monitors compare a selected ruler/checkerboard and saved/pasted dimensions. Verify negative-origin secondary displays and that top-level window selection does not include the tool's own UI.

## Task 3: 六种标注与可撤销编辑

**Files:** Create `src/ScreenshotTool/Editing/CaptureDocument.cs`, `src/ScreenshotTool/Editing/Annotation.cs`, `src/ScreenshotTool/Editing/DocumentRenderer.cs`, `src/ScreenshotTool/Editing/EditorView.xaml`, `src/ScreenshotTool/Editing/EditorView.xaml.cs`, `src/ScreenshotTool/Editing/EditorKeyRouter.cs`, `tests/ScreenshotTool.Tests/CaptureDocumentTests.cs`, `tests/ScreenshotTool.Tests/DocumentRendererTests.cs`, `tests/ScreenshotTool.Tests/EditorKeyRouterTests.cs`; modify capture overlay and coordinator to host the shared editor.

**Interfaces:**
- Consumes normalized snapshot selection from Task 2.
- Produces `CaptureDocument(BitmapSource source, PixelRect selection)` with `Add(Annotation)`, `Replace(Guid id, Annotation value)`, `Remove(Guid id)`, `SetSelection(PixelRect)`, `Undo()`, `Redo()`, `Clone(): CaptureDocument` and read-only `Annotations`/`Selection`. Annotation IDs remain stable when modifying the same object.
- Produces immutable annotation variants for pen, rectangle, ellipse, arrow, text and mosaic; positions and widths use source pixels. Each has `Guid Id`; specific geometry, color and text properties stay in `Annotation.cs`.
- Produces `DocumentRenderer.Render(CaptureDocument document): BitmapSource`, cropped to selection without flattening the editable source document.
- Produces `EditorKeyRouter.ShouldComplete(bool textInputFocused, bool imeComposing): bool`; completion requires both values false.

- [ ] Write `UndoRedo_RestoresModifiedText` using one text annotation changed from `原文` to `修改`; assert Undo returns original text and Redo returns modified text. Write add/remove and move cases for pen and rectangle.
- [ ] Write `Clone_EditDoesNotChangeOriginal`: modify the clone's text and assert `Assert.Equal("原文", originalText)` and the original annotation ID is unchanged.
- [ ] Write `Render_UsesSelectionSize` with selection `(10,20,80,40)`; assert `Assert.Equal(80, image.PixelWidth); Assert.Equal(40, image.PixelHeight);`. Verify selected sample pixels for a rectangle, arrow, ellipse and mosaic, including clipping after resizing the selection.
- [ ] Write `EnterInTextOrIme_DoesNotComplete`: assert false for `(true,false)` and `(false,true)`, true for `(false,false)`.
- [ ] Run red tests; implement the document, renderer and shared editor with hit-testing, select/move/delete, text modification, tool colors/widths/fonts, and undo/redo. Rendering and UI read the same annotation model.
- [ ] Run tests/build; manually verify all six tools, multiline Chinese text/IME, Ctrl+Z/Ctrl+Y and selection adjustments. Confirm mosaic is present in copied/rendered pixels, not merely in the UI overlay.

## Task 4: 输出工作流与基础置顶贴图

**Files:** Create `src/ScreenshotTool/Workflow/ScreenshotWorkflow.cs`, `src/ScreenshotTool/Workflow/ImageOutputs.cs`, `src/ScreenshotTool/Pins/PinManager.cs`, `src/ScreenshotTool/Pins/PinnedWindow.xaml`, `src/ScreenshotTool/Pins/PinnedWindow.xaml.cs`, `tests/ScreenshotTool.Tests/ScreenshotWorkflowTests.cs`, `tests/ScreenshotTool.Tests/ImageOutputsTests.cs`; modify clipboard service, coordinator and editor.

**Interfaces:**
- Consumes `CaptureDocument`, `DocumentRenderer.Render` and the output success/error concept from Task 1; consolidate the record as `OutputResult(bool Success, string? Error)` in `Workflow/ImageOutputs.cs` and update references.
- Produces `ImageFormat { Png, Jpeg }`, `CompletionAction { CopyOnly, PinOnly, CopyAndPin }` and `IImageOutputs.CopyAsync(BitmapSource image): Task<OutputResult>` / `SaveAsync(BitmapSource image, string path, ImageFormat format): Task<OutputResult>`.
- Produces `PinManager.Create(CaptureDocument document): Guid`, `Get(Guid id): CaptureDocument`, `Close(Guid id): void`, and `Count`. Manager takes a clone; UI displays one PinnedWindow per ID.
- Produces `WorkflowResult(bool ShouldClose, Guid? CreatedPinId, string? Error)` and `ScreenshotWorkflow.CompleteNewAsync(CaptureDocument document, CompletionAction action): Task<WorkflowResult>` / `SaveNewAsync(CaptureDocument document, string path, ImageFormat format): Task<WorkflowResult>`. Workflow owns one active submission at a time and remembers completion per active document to suppress repeated confirmation.

- [ ] Write `Complete_CopyAndPin_CreatesOnePin`: assert one copy, one pin, ShouldClose true. Cover CopyOnly `(1,0)` and PinOnly `(0,1)` for copy/pin counts.
- [ ] Write `SaveNew_Success_OnlySavesAndCloses`: assert saves `1`, copies `0`, pins `0`, ShouldClose true. Write failure counterpart with ShouldClose false; cancelling the file dialog invokes no output method and keeps the editor open.
- [ ] Write `ClipboardBusy_RetryDoesNotDuplicatePin`: fail first copy, assert pins `0` and editor retained; then succeed and submit the same completion twice, assert successful copies `1`, pins `1`.
- [ ] Add PNG/JPG round-trip tests against temporary files: assert exact dimensions; use exact pixels for PNG and tolerance for JPEG. Unwritable path yields failure with a retained document. Run tests red.
- [ ] Implement the workflow, save dialog wiring, output encoders, pin window Topmost/drag/zoom/opacity, and explicit Copy/Save/Close actions. Pin display transforms never enter the document renderer. Replace Task 1's direct copy completion with this workflow; enable the default CopyAndPin action.
- [ ] Run tests/build and verify the specification's new-screenshot result table in the actual UI; verify paste still works after returning to the tray.

## Task 5: 贴图原标注再次编辑与临时隐藏

**Files:** Create `src/ScreenshotTool/Pins/PinEditSession.cs`, `tests/ScreenshotTool.Tests/PinEditingTests.cs`, `tests/ScreenshotTool.Tests/PinVisibilityTests.cs`; modify `Pins/PinManager.cs`, pin window, shared editor, workflow and capture coordinator.

**Interfaces:**
- Consumes the document clone, pin IDs and shared editor from Tasks 3–4.
- Produces `PinEditSession(Guid PinId, CaptureDocument Draft)`; `PinManager.BeginEdit(Guid id): PinEditSession`, `Commit(PinEditSession edit): void`, `Cancel(PinEditSession edit): void`.
- Produces `ScreenshotWorkflow.SavePinDraftAsync(PinEditSession edit, string path, ImageFormat format): Task<WorkflowResult>`; successful draft export returns ShouldClose false and does not commit the draft.
- Produces `PinManager.HideForCapture(): IDisposable`; disposing restores the previous visibility state, including capture failure or cancellation. Closing a pin during the scope does not recreate it.

- [ ] Write `Commit_UpdatesSamePinWithoutCopy`: original text `原文` → draft `修改`; assert pin count `1`, same ID, changed text, copy count `0`.
- [ ] Write `Cancel_RestoresOriginalAnnotations`: move a rectangle and remove a pen stroke in the draft; cancel and assert original geometry and stroke still present.
- [ ] Write `ExportDraftThenCancel_LeavesFileAndOriginal`: export modified text to PNG, cancel; assert file exists with draft-rendered pixels, original text remains `原文`, copy count `0`, ShouldClose false for export.
- [ ] Write `CaptureExitPaths_RestorePins`: success, cancel and throwing capture all restore previously visible pins; already hidden pins stay hidden. Run tests red.
- [ ] Wire the pin Edit action to the shared editor with a separate draft. Enter/confirm commits, Esc/cancel discards, Ctrl+S exports the draft without leaving editing. Use try/finally to dispose the visibility scope on every capture exit path.
- [ ] Run tests/build; manually verify original labels remain individually selectable after reopening and multiple edits, pin count stays fixed, zoom/opacity do not change copied or saved image size, and restarting creates no pins.

## Task 6: 持久设置、改键、单实例和开机启动

**Files:** Create `src/ScreenshotTool/Settings/AppSettings.cs`, `src/ScreenshotTool/Settings/SettingsService.cs`, `src/ScreenshotTool/Settings/SettingsWindow.xaml`, `src/ScreenshotTool/Settings/SettingsWindow.xaml.cs`, `src/ScreenshotTool/Desktop/SingleInstance.cs`, `src/ScreenshotTool/Desktop/StartupRegistration.cs`, `tests/ScreenshotTool.Tests/SettingsServiceTests.cs`; modify App and tray wiring.

**Interfaces:**
- Consumes `Hotkey`, `IHotkeyRegistrar`, `CompletionAction`, `ImageFormat`.
- Produces `AppSettings(Hotkey CaptureHotkey, CompletionAction CompletionAction, ImageFormat ImageFormat, string? LastSaveDirectory, bool StartWithWindows)`.
- Produces `SettingsService(string settingsPath, IHotkeyRegistrar registrar)`, `Load(): AppSettings`, `TryApply(AppSettings candidate): RegistrationResult`, and `Current: AppSettings`. Persist only settings; never serialize capture documents or pins.
- Produces `SingleInstance.TryAcquire(): bool` and `SignalExisting(): void`; existing instance surfaces its settings/tray UI without starting another editor. `StartupRegistration.SetEnabled(bool enabled): void` only changes the current user's startup entry.

- [ ] Write `Defaults_MatchSpec`: assert Hotkey `(3,83)`, CopyAndPin, Png, StartWithWindows false. Write `Reload_PreservesSuccessfulChanges` using a temporary settings file.
- [ ] Write `OccupiedCandidate_PreservesOldHotkeyAndFile`: fake registration failure; assert failure result and old Current/file unchanged. Add malformed settings recovery to defaults with a visible settings notification and usable tray.
- [ ] Write `SuccessfulSave_RemembersDirectory` and `Settings_DoNotContainImages`: assert directory persisted only after success; serialized settings contain no capture or pin fields. Run tests red.
- [ ] Implement current-user settings persistence with replacement only after successful writes, hotkey rollback when necessary, completion-action/format settings, and startup checkbox. Acquire single-instance ownership before tray or hotkey creation.
- [ ] Run tests/build; manually verify immediate hotkey change and collision rollback, reload, single instance and optional startup. Normal exit closes pins without prompts or image persistence and unregisters native resources.

## Task 7: 自包含安装包与目标系统验收

**Files:** Create `installer/ScreenshotTool.iss`, `scripts/package.ps1`, `docs/testing/windows-acceptance.md`, `README.md`; modify application publish/version metadata only as needed.

**Interfaces:**
- Consumes the complete Release application from Tasks 1–6.
- Produces `scripts/package.ps1 -Configuration Release -Version <release-version>` which publishes Windows x64 with the runtime, then compiles the Inno Setup installer. It fails clearly if the SDK or compiler is missing; success produces `artifacts/installer/ScreenshotTool-Setup-x64.exe`.
- Produces a stable per-product installer identity for upgrades, current-user installation with `PrivilegesRequired=lowest`, Start menu/optional desktop shortcut, and uninstall entry. No machine-wide settings or administrator requirement.

- [ ] Obtain Inno Setup for build use and run `dotnet publish src/ScreenshotTool/ScreenshotTool.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64`. Check the published app launches independently of the SDK.
- [ ] Add package script and installer; preserve current-user settings on upgrade, close the running application before replacement, and leave user-exported images untouched on uninstall. Document the chosen settings retention behavior.
- [ ] Run the full test command, Release build and package script. Require exit code zero and the installer file to exist; record the release version and tool versions.
- [ ] On clean Windows 10 22H2 x64 and Windows 11 x64 without .NET, disconnect networking and install as a standard user. Verify no elevation prompt, start/shortcuts, default and customized hotkeys, all result-table actions, multiple editable pins and uninstall.
- [ ] Verify running-instance upgrade: set a non-default hotkey, save an image, leave a pin open, install a newer version; assert application replacement succeeds, settings and saved image remain, and startup does not restore pins.
- [ ] Record DPI/negative-origin/cross-screen, clipboard-busy, unwritable-directory, IME and capture-failure acceptance results with actual environment details. Do not mark untested systems or hardware scenarios as passed.
- [ ] Deliver the installer, source, build/package instructions and completed acceptance record. When Git is available, complete the branch review and task commits before proposing integration.

## 规格覆盖与自查

- User Stories 1–6 → Tasks 1、6；7–11 → Tasks 1、2；12–15 → Task 3。
- User Stories 16–20 → Task 4；21–28 → Tasks 4、5；29 → Tasks 5、6；30 → Tasks 1、4、5；31–32 → Task 7。
- 结果表逐行由 Tasks 4–5 验证；默认值由 Task 6 验证；真实系统与分发由 Task 7 验证。
- 同一边界的类型和方法沿任务顺序复用；Windows 专属代码不进入设置或工作流测试 fake。
- 每个任务先新增目标行为的失败验证，再实现并检查；输出与资源释放以用户可观察结果和系统集成验证为准。
- 本计划未执行产品实现。执行方式尚未选择；开始实现前选择当前会话直接执行或按任务使用子代理执行。
