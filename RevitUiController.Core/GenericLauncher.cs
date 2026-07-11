using System.Diagnostics;

namespace UiController.Core;

public class GenericLauncher : IApplicationLauncher
{
    private readonly IApplicationProfile _profile;

    public GenericLauncher(IApplicationProfile profile)
    {
        _profile = profile;
    }

    public Process? Launch(string? executablePath = null)
    {
        var path = executablePath ?? _profile.ProcessName;
        var psi = new ProcessStartInfo(path) { UseShellExecute = true };
        return Process.Start(psi);
    }

    public Process? FindRunning()
    {
        return Process.GetProcessesByName(_profile.ProcessName)
            .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
    }

    public async Task<bool> WaitForReady(int timeoutMs, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var process = FindRunning();
            if (process != null)
                return true;
            await Task.Delay(1000, ct);
        }
        return false;
    }

    public bool IsAlive(Process? process)
    {
        if (process == null) return false;
        try { return !process.HasExited; }
        catch { return false; }
    }
}
