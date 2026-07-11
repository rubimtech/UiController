using System.Diagnostics;

namespace UiController.Core;

public interface IApplicationLauncher
{
    Process? Launch(string? executablePath = null);
    Process? FindRunning();
    Task<bool> WaitForReady(int timeoutMs, CancellationToken ct = default);
    bool IsAlive(Process? process);
}
