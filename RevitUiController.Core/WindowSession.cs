using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using RevitUiController.Core.Models;

namespace RevitUiController.Core;

public class WindowSession : IDisposable
{
    public UIA3Automation Automation { get; }
    public AutomationElement MainWindow { get; private set; }
    public Process Process { get; }
    public int? TargetPid { get; }
    public WindowInfo? WindowInfo { get; private set; }
    public IAutomationProvider? Provider { get; }

    private WindowSession(UIA3Automation automation, AutomationElement mainWindow, Process process, int? targetPid = null, WindowInfo? info = null)
    {
        Automation = automation;
        MainWindow = mainWindow;
        Process = process;
        TargetPid = targetPid;
        WindowInfo = info;
    }

    private WindowSession(IAutomationProvider provider, AutomationElement mainWindow, Process process, int? targetPid = null, WindowInfo? info = null)
    {
        Provider = provider;
        Automation = provider.UIA3 ?? new UIA3Automation();
        MainWindow = mainWindow;
        Process = process;
        TargetPid = targetPid;
        WindowInfo = info;
    }

    public static async Task<WindowSession?> ConnectToProcess(int? targetPid = null, string processName = "Revit", int timeoutSec = 30, CancellationToken ct = default)
    {
        return await ConnectToProcess(new UIA3AutomationProvider(), targetPid, processName, timeoutSec, ct);
    }

    public static async Task<WindowSession?> ConnectToProcess(IAutomationProvider provider, int? targetPid = null, string processName = "Revit", int timeoutSec = 30, CancellationToken ct = default)
    {
        Process? process = null;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);

        if (targetPid.HasValue)
        {
            process = await FindProcessByPid(targetPid.Value, processName, deadline, ct);
            if (process == null) return null;
        }
        else
        {
            process = await FindFirstProcess(processName, deadline, ct);
            if (process == null) return null;
        }

        return ConnectToHandle(process.MainWindowHandle, provider, process, targetPid);
    }

    public static WindowSession? ConnectToHandle(IntPtr hWnd, Process? existingProcess = null, int? targetPid = null)
    {
        return ConnectToHandle(hWnd, new UIA3AutomationProvider(), existingProcess, targetPid);
    }

    public static WindowSession? ConnectToHandle(IntPtr hWnd, IAutomationProvider provider, Process? existingProcess = null, int? targetPid = null)
    {
        try
        {
            var window = provider.GetRootElement(hWnd);
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

                var winInfo = ActiveWindowTracker.WindowFromHandle(hWnd);
                LoggingService.Info("WindowSession", $"Connected to \"{window.Name}\" PID={process?.Id ?? 0}");
                return new WindowSession(provider, window, process!, targetPid, winInfo);
            }
        }
        catch (Exception ex)
        {
            LoggingService.Error("WindowSession", $"Failed to connect to window: {ex.Message}");
        }

        LoggingService.Warn("WindowSession", "Window not found via UIA.");
        return null;
    }

    public static async Task<WindowSession?> ConnectToActive(int timeoutSec = 5, CancellationToken ct = default)
    {
        return await ConnectToActive(new UIA3AutomationProvider(), timeoutSec, ct);
    }

    public static async Task<WindowSession?> ConnectToActive(IAutomationProvider provider, int timeoutSec = 5, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                var session = ConnectToHandle(hwnd, provider);
                if (session != null) return session;
            }
            await Task.Delay(300, ct);
        }
        LoggingService.Error("WindowSession", "No active window found.");
        return null;
    }

    public static async Task<WindowSession?> ConnectByTitle(string title, int timeoutSec = 30, CancellationToken ct = default)
    {
        return await ConnectByTitle(title, new UIA3AutomationProvider(), timeoutSec, ct);
    }

    public static async Task<WindowSession?> ConnectByTitle(string title, IAutomationProvider provider, int timeoutSec = 30, CancellationToken ct = default)
    {
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

            if (found != IntPtr.Zero)
            {
                var session = ConnectToHandle(found, provider);
                if (session != null) return session;
            }
            await Task.Delay(500, ct);
        }
        LoggingService.Warn("WindowSession", $"Window with title '{title}' not found.");
        return null;
    }

    public static async Task<WindowSession?> Resolve(WindowQuery query, int timeoutSec = 30, CancellationToken ct = default)
    {
        return await Resolve(query, new UIA3AutomationProvider(), timeoutSec, ct);
    }

    public static async Task<WindowSession?> Resolve(WindowQuery query, IAutomationProvider provider, int timeoutSec = 30, CancellationToken ct = default)
    {
        var defaultProcessName = CoreSettings.CurrentProfile.ProcessName;
        if (query.UseActive)
            return await ConnectToActive(provider, ct: ct);
        if (query.Pid.HasValue)
            return await ConnectToProcess(provider, query.Pid, timeoutSec: timeoutSec, ct: ct);
        if (!string.IsNullOrEmpty(query.ProcessName))
            return await ConnectToProcess(provider, processName: query.ProcessName, timeoutSec: timeoutSec, ct: ct);
        if (!string.IsNullOrEmpty(query.WindowTitle))
            return await ConnectByTitle(query.WindowTitle, provider, timeoutSec, ct);
        return await ConnectToProcess(provider, processName: defaultProcessName, timeoutSec: timeoutSec, ct: ct);
    }

    public void RefreshWindow()
    {
        try
        {
            if (Provider != null)
                MainWindow = Provider.GetRootElement(Process.MainWindowHandle);
            else
                MainWindow = Automation.FromHandle(Process.MainWindowHandle);
        }
        catch { }
    }

    public void Dispose()
    {
        Automation?.Dispose();
    }

    private static async Task<Process?> FindProcessByPid(int pid, string processName, DateTime deadline, CancellationToken ct = default)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            if (p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
            {
                if (await WaitForMainWindow(p, deadline, ct)) return p;
            }
            else
            {
                LoggingService.Error("WindowSession", $"PID {pid} is not a {processName} process (found: {p.ProcessName}).");
                return null;
            }
        }
        catch (ArgumentException)
        {
            LoggingService.Warn("WindowSession", $"Process with PID {pid} not found, waiting up to {(deadline - DateTime.UtcNow).TotalSeconds:F0}s...");
        }

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

        LoggingService.Warn("WindowSession", $"Process PID={pid} not found after timeout.");
        return null;
    }

    private static async Task<Process?> FindFirstProcess(string processName, DateTime deadline, CancellationToken ct = default)
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

        LoggingService.Warn("WindowSession", $"{processName} process not found.");
        return null;
    }

    private static async Task<bool> WaitForMainWindow(Process process, DateTime deadline, CancellationToken ct = default)
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
