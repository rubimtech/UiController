using System.Diagnostics;
using System.IO;
using RevitUiController.Core;

namespace RevitUiController.Revit;

public class RevitLauncher : IApplicationLauncher
{
    private readonly IApplicationProfile _profile;

    public RevitLauncher(IApplicationProfile profile)
    {
        _profile = profile;
    }

    public Process? Launch(string? executablePath = null)
    {
        if (executablePath == null)
        {
            executablePath = _profile.ExecutablePaths.FirstOrDefault(File.Exists);
        }
        if (executablePath == null || !File.Exists(executablePath))
            return null;

        var psi = new ProcessStartInfo(executablePath) { UseShellExecute = true };
        return Process.Start(psi);
    }

    public Process? FindRunning()
    {
        return Process.GetProcessesByName("Revit")
            .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
    }

    public async Task<bool> WaitForReady(int timeoutMs, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var process = FindRunning();
            if (process != null && process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
                return true;
            await Task.Delay(2000, ct);
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
