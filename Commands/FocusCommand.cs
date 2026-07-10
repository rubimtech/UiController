using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class FocusCommand : ICommand
{
    public string Name => "focus";
    public string Description => "Bring a window to foreground by title or by PID with --pid <N>. Usage: focus <title> or focus --pid <N> or focus --hwnd <hex>";
    public string Usage => "focus <title> [--pid <N> | --hwnd <hex>]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var mgr = Program.WindowManager;
        if (mgr == null)
        {
            Console.Write(OutputFormatter.FormatError("NoWindowManager", "", null, Program.IsPretty));
            return Task.FromResult(1);
        }

        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: focus <title> [--pid <N> | --hwnd <hex>]");
            return Task.FromResult(1);
        }

        int? targetPid = null;
        long? targetHwnd = null;
        string? titleFilter = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--pid" && i + 1 < args.Length && int.TryParse(args[++i], out var p))
                targetPid = p;
            else if (args[i] == "--hwnd" && i + 1 < args.Length)
                targetHwnd = Convert.ToInt64(args[++i], 16);
            else
                titleFilter = (titleFilter == null ? args[i] : titleFilter + " " + args[i]);
        }

        var windows = mgr.GetAllWindows();
        WindowInfo? target = null;

        if (targetHwnd.HasValue)
        {
            target = windows.FirstOrDefault(w => w.Hwnd.ToInt64() == targetHwnd.Value);
        }
        else if (targetPid.HasValue)
        {
            target = windows.FirstOrDefault(w => w.Pid == targetPid.Value);
        }
        else if (titleFilter != null)
        {
            target = windows.FirstOrDefault(w =>
                w.Title.Contains(titleFilter, StringComparison.OrdinalIgnoreCase));
        }

        if (target == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", titleFilter ?? $"pid={targetPid}", null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var success = mgr.FocusWindow(target.Hwnd);

        var result = new CommandResult
        {
            Command = "focus",
            Success = success,
            Error = success ? null : $"Failed to focus window '{target.Title}'",
            Data = new
            {
                pid = target.Pid,
                process = target.ProcessName,
                title = target.Title,
                hwnd = $"0x{target.Hwnd.ToInt64():X8}",
                monitor = target.MonitorName
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(success ? 0 : 1);
    }
}
