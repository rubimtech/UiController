using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace RevitUiController.Core.Services;

public class AutomationService : IAutomationService
{
    private readonly IAutomationProvider _provider;
    private UIA3Automation? _automation;
    private AutomationElement? _mainWindow;
    private Process? _process;

    public AutomationService()
        : this(new UIA3AutomationProvider())
    {
    }

    public AutomationService(IAutomationProvider provider)
    {
        _provider = provider;
    }

    public IAutomationProvider Provider => _provider;
    public UIA3Automation? Automation => _automation ?? _provider.UIA3;
    public AutomationElement? MainWindow => _mainWindow;
    public bool IsConnected => _mainWindow != null;
    public int? TargetPid { get; private set; }

    public async Task<bool> ConnectToProcess(int? targetPid = null, string processName = "Revit", int timeoutSec = 30, CancellationToken ct = default)
    {
        Disconnect();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);

        Process? process = null;

        if (targetPid.HasValue)
        {
            process = await FindProcessByPid(targetPid.Value, processName, deadline, ct);
        }
        else
        {
            process = await FindFirstProcess(processName, deadline, ct);
        }

        if (process == null) return false;
        return AttachToProcess(process, targetPid);
    }

    public async Task<bool> ConnectToActive(CancellationToken ct = default)
    {
        Disconnect();
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        return AttachToHandle(hwnd);
    }

    public async Task<bool> ConnectByTitle(string title, int timeoutSec = 30, CancellationToken ct = default)
    {
        Disconnect();
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            IntPtr found = IntPtr.Zero;
            NativeMethods.EnumWindows((hWnd, _) =>
            {
                if (found != IntPtr.Zero) return true;
                if (!NativeMethods.IsWindowVisible(hWnd)) return true;
                var t = NativeMethods.GetWindowTitle(hWnd);
                if (t.Contains(title, StringComparison.OrdinalIgnoreCase))
                    found = hWnd;
                return true;
            }, IntPtr.Zero);

            if (found != IntPtr.Zero && AttachToHandle(found))
                return true;

            await Task.Delay(500, ct);
        }
        return false;
    }

    public void Disconnect()
    {
        _automation?.Dispose();
        _automation = null;
        _mainWindow = null;
        _process = null;
        TargetPid = null;
    }

    private bool AttachToProcess(Process process, int? targetPid = null)
    {
        return AttachToHandle(process.MainWindowHandle, process, targetPid);
    }

    private bool AttachToHandle(IntPtr hWnd, Process? existingProcess = null, int? targetPid = null)
    {
        try
        {
            var window = _provider.GetRootElement(hWnd);
            if (window != null && !string.IsNullOrEmpty(window.Name))
            {
                Process? process = existingProcess;
                if (process == null)
                {
                    try
                    {
                        var pid = NativeMethods.GetProcessId(hWnd);
                        process = Process.GetProcessById((int)pid);
                    }
                    catch { }
                }

                _automation = _provider.UIA3;
                _mainWindow = window;
                _process = process;
                TargetPid = targetPid;
                return true;
            }
        }
        catch { }
        return false;
    }

    public void Dispose()
    {
        Disconnect();
    }

    private static async Task<Process?> FindProcessByPid(int pid, string processName, DateTime deadline, CancellationToken ct)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            if (p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
            {
                if (await WaitForMainWindow(p, deadline, ct)) return p;
            }
            return null;
        }
        catch (ArgumentException) { }

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var p = Process.GetProcessById(pid);
                if (p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                {
                    if (await WaitForMainWindow(p, deadline, ct)) return p;
                }
                await Task.Delay(500, ct);
            }
            catch (ArgumentException) { await Task.Delay(500, ct); }
        }
        return null;
    }

    private static async Task<Process?> FindFirstProcess(string processName, DateTime deadline, CancellationToken ct)
    {
        var processes = Process.GetProcessesByName(processName)
            .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
            .OrderByDescending(p => p.MainWindowTitle.Length)
            .ToArray();

        if (processes.Length > 0) return processes[0];

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            await Task.Delay(500, ct);
            processes = Process.GetProcessesByName(processName)
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .OrderByDescending(p => p.MainWindowTitle.Length)
                .ToArray();
            if (processes.Length > 0) return processes[0];
        }
        return null;
    }

    private static async Task<bool> WaitForMainWindow(Process process, DateTime deadline, CancellationToken ct)
    {
        if (process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
            return true;

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
                return true;
            await Task.Delay(500, ct);
        }
        return false;
    }
}
