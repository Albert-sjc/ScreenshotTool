using System;
using System.Threading;
using System.Runtime.InteropServices;
using ScreenshotTool.Capture;
using ScreenshotTool.Desktop;
using Xunit;

namespace ScreenshotTool.Tests;

[CollectionDefinition("NativeDesktop", DisableParallelization = true)]
public class NativeDesktopCollection { }

[Collection("NativeDesktop")]
public class NativeIntegrationTests
{
    [Fact]
    public void HotkeyCollision_KeepsTheOldRegistrationActive()
    {
        Sta.Run(() =>
        {
            using var owner = new HotkeyService();
            using var blocker = new HotkeyService();
            using var observer = new HotkeyService();
            var old = new Hotkey(3, 0x86);
            var occupied = new Hotkey(3, 0x87);
            Assert.True(owner.TryRegister(old).Success);
            Assert.True(blocker.TryRegister(occupied).Success);
            Assert.False(owner.TryRegister(occupied).Success);
            Assert.False(observer.TryRegister(old).Success);
            owner.Dispose();
            Assert.True(observer.TryRegister(old).Success);
        });
    }

    [Fact]
    public void Snapshot_CoversTheVirtualDesktopAndReleasesGdiResources()
    {
        Sta.Run(() =>
        {
            var expected = PhysicalDesktopBounds();
            _ = new ScreenCaptureService().Capture();
            var process = System.Diagnostics.Process.GetCurrentProcess();
            var before = GetGuiResources(process.Handle, 0);
            ScreenSnapshot? last = null;
            for (var index = 0; index < 4; index++) last = new ScreenCaptureService().Capture();
            Assert.NotNull(last);
            Assert.True(last.Pixels.IsFrozen);
            Assert.Equal(expected.X, last.Origin.X);
            Assert.Equal(expected.Y, last.Origin.Y);
            Assert.Equal(expected.Width, last.Pixels.PixelWidth);
            Assert.Equal(expected.Height, last.Pixels.PixelHeight);
            Assert.InRange(GetGuiResources(process.Handle, 0), 0, before + 2);
        });
    }

    [Fact]
    public void SingleInstance_SecondOwnerSignalsTheFirst()
    {
        var name = "ScreenshotTool.Tests." + Guid.NewGuid().ToString("N");
        using var first = new SingleInstance(name);
        using var second = new SingleInstance(name);
        using var signaled = new ManualResetEventSlim();
        first.ExistingInstanceSignaled += (_, _) => signaled.Set();
        Assert.True(first.TryAcquire());
        Assert.False(second.TryAcquire());
        second.SignalExisting();
        Assert.True(signaled.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    private static PixelRect PhysicalDesktopBounds()
    {
        // EnumDisplaySettings is never DPI-virtualized. This provides an independent
        // physical-pixel baseline even when the test host is only system-DPI-aware.
        var left = int.MaxValue;
        var top = int.MaxValue;
        var right = int.MinValue;
        var bottom = int.MinValue;
        for (uint index = 0; ; index++)
        {
            var device = new DisplayDevice { Size = Marshal.SizeOf<DisplayDevice>() };
            if (!EnumDisplayDevices(null, index, ref device, 0)) break;
            if ((device.Flags & 1) == 0) continue;
            var mode = new DisplayMode { Size = (ushort)Marshal.SizeOf<DisplayMode>() };
            Assert.True(EnumDisplaySettings(device.Name, -1, ref mode));
            left = Math.Min(left, mode.X);
            top = Math.Min(top, mode.Y);
            right = Math.Max(right, mode.X + mode.Width);
            bottom = Math.Max(bottom, mode.Y + mode.Height);
        }
        Assert.NotEqual(int.MaxValue, left);
        return new(left, top, right - left, bottom - top);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DisplayDevice
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }

    [StructLayout(LayoutKind.Explicit, Size = 220)]
    private struct DisplayMode
    {
        [FieldOffset(68)] public ushort Size;
        [FieldOffset(76)] public int X;
        [FieldOffset(80)] public int Y;
        [FieldOffset(172)] public int Width;
        [FieldOffset(176)] public int Height;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string? name, uint index, ref DisplayDevice device, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplaySettings(string name, int mode, ref DisplayMode device);
    [DllImport("user32.dll")] private static extern int GetGuiResources(nint process, int flags);
}
