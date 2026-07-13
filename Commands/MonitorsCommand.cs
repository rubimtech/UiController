using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class MonitorsCommand : ICommand
{
    public string Name => "monitors";
    public string Description => "List all monitors with resolution, DPI, work area, and primary flag";
    public string Usage => "monitors";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var mgr = Program.WindowManager;
        if (mgr == null)
        {
            Console.Write(OutputFormatter.FormatError("NoWindowManager", "", null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var monitors = mgr.GetAllMonitors();

        var result = new CommandResult
        {
            Command = "monitors",
            Success = true,
            Data = Program.Verbosity == "minimal"
                ? new { count = monitors.Count }
                : new
                {
                    count = monitors.Count,
                    monitors = monitors.Select(m => new
                    {
                        index = m.Index,
                        name = m.DeviceName,
                        primary = m.IsPrimary,
                        dpiScale = m.DpiScale,
                        bounds = new { x = m.X, y = m.Y, w = m.Width, h = m.Height },
                        workArea = new { x = m.WorkX, y = m.WorkY, w = m.WorkWidth, h = m.WorkHeight }
                    }).ToList()
                }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}
