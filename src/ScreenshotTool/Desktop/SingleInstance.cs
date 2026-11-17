using System;
using System.Security.Principal;
using System.Threading;

namespace ScreenshotTool.Desktop;

public sealed class SingleInstance : IDisposable
{
    private readonly string mutexName;
    private readonly EventWaitHandle signal;
    private Mutex? mutex;
    private RegisteredWaitHandle? listener;
    private bool disposed;

    public SingleInstance(string instanceName = "ScreenshotTool")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(instanceName);
        using var identity = WindowsIdentity.GetCurrent();
        var prefix = @"Local\" + instanceName + "." + identity.User?.Value;
        mutexName = prefix + ".Instance";
        signal = new EventWaitHandle(false, EventResetMode.AutoReset, prefix + ".Activate");
    }

    public event EventHandler? ExistingInstanceSignaled;
    public bool TryAcquire()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (mutex is not null) return true;
        var candidate = new Mutex(false, mutexName, out var created);
        if (!created)
        {
            candidate.Dispose();
            return false;
        }
        mutex = candidate;
        listener = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) =>
        {
            if (!disposed) ExistingInstanceSignaled?.Invoke(this, EventArgs.Empty);
        }, null, Timeout.Infinite, executeOnlyOnce: false);
        return true;
    }

    public void SignalExisting()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        signal.Set();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        listener?.Unregister(null);
        signal.Dispose();
        mutex?.Dispose();
        mutex = null;
    }
}
