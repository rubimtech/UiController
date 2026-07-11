using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class ListAllWindowsCommand : ICommand
{
    public string Name => "list-all";
    public string Description => "List all visible top-level windows on the desktop with monitor info";
    public string Usage => "list-all (la) [--filter <text>]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        string? filter = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--filter" && i + 1 < args.Length)
                filter = args[++i];
        }

        var mgr = CoreSettings.WindowManager;
        if (mgr == null)
        {
            Console.Write(OutputFormatter.FormatError("NoWindowManager", "", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var windows = mgr.GetAllWindows();
        if (filter != null)
            windows = windows.Where(w => w.Title.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                                         w.ProcessName.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        var result = new CommandResult
        {
            Command = "list-all",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { count = windows.Count }
                : new { count = windows.Count, windows = windows.Select(w => new
                  {
                      pid = w.Pid,
                      process = w.ProcessName,
                      title = Truncate(w.Title, 80),
                      hwnd = $"0x{w.Hwnd.ToInt64():X8}",
                      bounds = new { x = w.X, y = w.Y, w = w.Width, h = w.Height },
                      monitor = w.MonitorName,
                      foreground = w.IsForeground,
                      minimized = w.IsMinimized
                  }).ToList() }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 3)] + "...";
}
