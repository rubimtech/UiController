using System.Diagnostics;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace RevitUiController;

public class RevitSession : IDisposable
{
    public UIA3Automation Automation { get; }
    public AutomationElement MainWindow { get; private set; }
    public Process Process { get; }
    public int? TargetPid { get; }

    private RevitSession(UIA3Automation automation, AutomationElement mainWindow, Process process, int? targetPid = null)
    {
        Automation = automation;
        MainWindow = mainWindow;
        Process = process;
        TargetPid = targetPid;
    }

    public static RevitSession? Connect(int? targetPid = null, string processName = "Revit", int timeoutSec = 30)
    {
        Process? process = null;
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSec);

        if (targetPid.HasValue)
        {
            process = FindProcessByPid(targetPid.Value, processName, deadline);
            if (process == null)
                return null;
        }
        else
        {
            process = FindFirstProcess(processName, deadline);
            if (process == null)
                return null;
        }

        var automation = new UIA3Automation();
        try
        {
            var window = automation.FromHandle(process.MainWindowHandle);
            if (window != null && !string.IsNullOrEmpty(window.Name))
            {
                Console.Error.WriteLine($"Connected to {processName} PID={process.Id} \"{window.Name}\"");
                return new RevitSession(automation, window, process, targetPid);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to connect to PID={process.Id}: {ex.Message}");
        }

        automation.Dispose();
        Console.Error.WriteLine($"{processName} main window not found via UIA.");
        return null;
    }

    private static Process? FindProcessByPid(int pid, string processName, DateTime deadline)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            if (p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
            {
                if (WaitForMainWindow(p, deadline))
                    return p;
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
            try
            {
                var p = Process.GetProcessById(pid);
                if (p.ProcessName.Equals(processName, StringComparison.OrdinalIgnoreCase))
                {
                    if (WaitForMainWindow(p, deadline))
                        return p;
                }
                Thread.Sleep(500);
            }
            catch (ArgumentException)
            {
                Thread.Sleep(500);
            }
        }

        Console.Error.WriteLine($"Process PID={pid} not found after timeout.");
        return null;
    }

    private static Process? FindFirstProcess(string processName, DateTime deadline)
    {
        var processes = Process.GetProcessesByName(processName)
            .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
            .OrderByDescending(p => p.MainWindowTitle.Length)
            .ToArray();

        if (processes.Length > 0)
            return processes[0];

        while (DateTime.UtcNow < deadline)
        {
            Thread.Sleep(500);
            processes = Process.GetProcessesByName(processName)
                .Where(p => p.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(p.MainWindowTitle))
                .OrderByDescending(p => p.MainWindowTitle.Length)
                .ToArray();
            if (processes.Length > 0)
                return processes[0];
        }

        Console.Error.WriteLine($"{processName} process not found.");
        return null;
    }

    private static bool WaitForMainWindow(Process process, DateTime deadline)
    {
        if (process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
            return true;

        while (DateTime.UtcNow < deadline)
        {
            process.Refresh();
            if (process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
                return true;
            Thread.Sleep(500);
        }

        return false;
    }

    public void RefreshWindow()
    {
        try
        {
            MainWindow = Automation.FromHandle(Process.MainWindowHandle);
        }
        catch { }
    }

    public void Dispose()
    {
        Automation?.Dispose();
    }
}
