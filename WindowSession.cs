using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using RevitUiController.Models;

namespace RevitUiController;

public class WindowSession : IDisposable
{
    public UIA3Automation Automation { get; }
    public AutomationElement MainWindow { get; private set; }
    public Process Process { get; }
    public int? TargetPid { get; }
    public WindowInfo? WindowInfo { get; private set; }

    private WindowSession(UIA3Automation automation, AutomationElement mainWindow, Process process, int? targetPid = null, WindowInfo? info = null)
    {
        Automation = automation;
        MainWindow = mainWindow;
        Process = process;
        TargetPid = targetPid;
        WindowInfo = info;
    }

    public static async Task<WindowSession?> ConnectToProcess(int? targetPid = null, string processName = "Revit", int timeoutSec = 30, CancellationToken ct = default)
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

        return ConnectToHandle(process.MainWindowHandle, process, targetPid);
    }

    public static WindowSession? ConnectToHandle(IntPtr hWnd, Process? existingProcess = null, int? targetPid = null)
    {
        var automation = new UIA3Automation();
        try
        {
            var window = automation.FromHandle(hWnd);
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
                    catch (Exception ex) { LoggingService.Warn("Safe", $"ConnectToHandle GetProcessId: {ex.Message}"); }
                }

                var winInfo = ActiveWindowTracker.WindowFromHandle(hWnd);
                Console.Error.WriteLine($"Connected to \"{window.Name}\" PID={process?.Id ?? 0}");
                return new WindowSession(automation, window, process!, targetPid, winInfo);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to connect to window: {ex.Message}");
        }

        automation.Dispose();
        Console.Error.WriteLine("Window not found via UIA.");
        return null;
    }

    public static async Task<WindowSession?> ConnectToActive(int timeoutSec = 5, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var hwnd = NativeMethods.GetForegroundWindow();
            if (hwnd != IntPtr.Zero)
            {
                var session = ConnectToHandle(hwnd);
                if (session != null) return session;
            }
            await Task.Delay(300, ct);
        }
        Console.Error.WriteLine("No active window found.");
        return null;
    }

    public static async Task<WindowSession?> ConnectByTitle(string title, int timeoutSec = 30, CancellationToken ct = default)
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
                var session = ConnectToHandle(found);
                if (session != null) return session;
            }
            await Task.Delay(500, ct);
        }
        Console.Error.WriteLine($"Window with title '{title}' not found.");
        return null;
    }

    public static async Task<WindowSession?> Resolve(WindowQuery query, int timeoutSec = 30, CancellationToken ct = default)
    {
        if (query.UseActive)
            return await ConnectToActive(ct: ct);
        if (query.Pid.HasValue)
            return await ConnectToProcess(query.Pid, timeoutSec: timeoutSec, ct: ct);
        if (!string.IsNullOrEmpty(query.ProcessName))
            return await ConnectToProcess(processName: query.ProcessName, timeoutSec: timeoutSec, ct: ct);
        if (!string.IsNullOrEmpty(query.WindowTitle))
            return await ConnectByTitle(query.WindowTitle, timeoutSec, ct);
        return await ConnectToProcess(processName: "Revit", timeoutSec: timeoutSec, ct: ct);
    }

    public void RefreshWindow()
    {
        try
        {
            MainWindow = Automation.FromHandle(Process.MainWindowHandle);
        }
        catch (Exception ex) { LoggingService.Warn("Safe", $"RefreshWindow: {ex.Message}"); }
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
                Console.Error.WriteLine($"PID {pid} is not a {processName} process (found: {p.ProcessName}).");
                return null;
            }
        }
        catch (ArgumentException)
        {
            Console.Error.WriteLine($"Process with PID {pid} not found, waiting up to {(deadline - DateTime.UtcNow).TotalSeconds:F0}s...");
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

        Console.Error.WriteLine($"Process PID={pid} not found after timeout.");
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

        Console.Error.WriteLine($"{processName} process not found.");
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
