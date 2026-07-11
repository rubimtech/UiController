using RevitUiController.Core;
using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Revit.Commands;

public class ProcessListCommand : ICommand
{
    public string Name => "process-list";
    public string Description => "List running Revit processes with window handles";
    public string Usage => "process-list";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var processNames = new[] { "Revit", "RevitDBG", "RevitRCL" };
        var processes = new List<object>();

        foreach (var name in processNames)
        {
            foreach (var p in Process.GetProcessesByName(name))
            {
                try
                {
                    string? version = null;
                    try
                    {
                        version = p.MainModule?.FileVersionInfo?.FileVersion;
                    }
                    catch { }

                    processes.Add(new
                    {
                        pid = p.Id,
                        name = p.ProcessName,
                        title = p.MainWindowTitle ?? "",
                        hasWindow = p.MainWindowHandle != IntPtr.Zero,
                        windowHandle = p.MainWindowHandle != IntPtr.Zero
                            ? $"0x{p.MainWindowHandle:X8}"
                            : null,
                        version
                    });
                }
                catch { }
            }
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "process-list",
            Success = true,
            Data = new { count = processes.Count, processes }
        }, CoreSettings.GlobalOptions));

        return Task.FromResult(0);
    }
}

public class ProcessInfoCommand : ICommand
{
    public string Name => "process-info";
    public string Description => "Show detailed info about connected Revit process";
    public string Usage => "process-info";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var session = CoreSettings.CurrentSession;
        if (session?.Process == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "process-info",
                Success = false,
                Error = "No connected Revit session"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var p = session.Process;
        string? version = null;
        try { version = p.MainModule?.FileVersionInfo?.FileVersion; } catch { }
        string? startTime = null;
        try { startTime = p.StartTime.ToString("yyyy-MM-dd HH:mm:ss"); } catch { }
        long? memoryMb = null;
        try { memoryMb = p.WorkingSet64 / (1024 * 1024); } catch { }
        long? privateMemoryMb = null;
        try { privateMemoryMb = p.PrivateMemorySize64 / (1024 * 1024); } catch { }
        TimeSpan? uptime = null;
        try { uptime = DateTime.Now - p.StartTime; } catch { }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "process-info",
            Success = true,
            Data = new
            {
                pid = p.Id,
                name = p.ProcessName,
                title = p.MainWindowTitle ?? "",
                windowHandle = $"0x{p.MainWindowHandle:X8}",
                version,
                startTime,
                uptime = uptime?.ToString(@"d\.hh\:mm\:ss"),
                memoryMb,
                privateMemoryMb,
                responding = p.Responding,
                sessionId = p.SessionId,
                targetPid = session.TargetPid
            }
        }, CoreSettings.GlobalOptions));

        return Task.FromResult(0);
    }
}
