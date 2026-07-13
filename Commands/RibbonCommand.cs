using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Commands;

public class RibbonCommand : ICommand
{
    public string Name => "ribbon";
    public string Description => "Click a ribbon button, optionally after switching to a tab";
    public string Usage => "ribbon <button-name> [tab-name]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length >= 2)
        {
            var tabName = args[^1];
            var searchName = string.Join(" ", args.Take(args.Length - 1));

            var tab = FindFirstEnabledVisible(revitWindow, tabName);
            if (tab == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", $"tab '{tabName}'", null, Program.GlobalOptions));
                return 1;
            }

            var before = OutputFormatter.CaptureState(revitWindow);
            TryClick(tab, tabName);
            await Task.Delay(300, ct);

            var button = FindFirstEnabledVisible(revitWindow, searchName);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", searchName, null, Program.GlobalOptions));
                return 1;
            }
            TryClick(button, searchName);

            var after = OutputFormatter.CaptureState(revitWindow);
            var diff = OutputFormatter.ComputeDiff(before, after);

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var result = new CommandResult
            {
                Command = "ribbon",
                Success = true,
                Diff = diff,
                Data = Program.Verbosity == "minimal" ? null : new { button = searchName, tab = tabName },
                DurationMs = elapsed
            };
            if (Program.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
            Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        }
        else
        {
            var name = args[0];
            var button = FindFirstEnabledVisible(revitWindow, name);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.GlobalOptions));
                return 1;
            }

            var before = OutputFormatter.CaptureState(revitWindow);
            TryClick(button, name);
            var after = OutputFormatter.CaptureState(revitWindow);
            var diff = OutputFormatter.ComputeDiff(before, after);

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var result = new CommandResult
            {
                Command = "ribbon",
                Success = true,
                Diff = diff,
                Data = Program.Verbosity == "minimal" ? null : new { button = name },
                DurationMs = elapsed
            };
            if (Program.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
            Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        }
        return 0;
    }
}
