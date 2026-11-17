using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace ScreenshotTool.Tests;

public static class Sta
{
    public static void Run(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try { action(); }
            catch (Exception error) { failure = error; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        if (!thread.Join(TimeSpan.FromSeconds(30))) throw new TimeoutException("STA 测试未在 30 秒内完成。");
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
