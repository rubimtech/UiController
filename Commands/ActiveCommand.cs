using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class ActiveCommand : ICommand
{
    public string Name => "active";
    public string Description => "Show info about the currently active/foreground window and its monitor";
    public string Usage => "active";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var mgr = Program.WindowManager;
        if (mgr == null)
        {
            Console.Write(OutputFormatter.FormatError("NoWindowManager", "", null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var info = mgr.GetActiveWindowInfo();
        if (info == null)
        {
            Console.Write(OutputFormatter.FormatError("NoActiveWindow", "", null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var monitors = mgr.GetAllMonitors();
        var monitor = monitors.FirstOrDefault(m => m.DeviceName == info.MonitorName);

        var result = new CommandResult
        {
            Command = "active",
            Success = true,
            Data = new
            {
                hwnd = $"0x{info.Hwnd.ToInt64():X8}",
                title = info.Title,
                pid = info.Pid,
                process = info.ProcessName,
                bounds = new { x = info.X, y = info.Y, w = info.Width, h = info.Height },
                minimized = info.IsMinimized,
                monitor = monitor == null ? null : new
                {
                    index = monitor.Index,
                    name = monitor.DeviceName,
                    primary = monitor.IsPrimary,
                    dpiScale = monitor.DpiScale,
                    bounds = new { x = monitor.X, y = monitor.Y, w = monitor.Width, h = monitor.Height },
                    workArea = new { x = monitor.WorkX, y = monitor.WorkY, w = monitor.WorkWidth, h = monitor.WorkHeight }
                }
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}
