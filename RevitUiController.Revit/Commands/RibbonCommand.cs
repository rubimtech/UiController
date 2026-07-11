using UiController.Core;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Revit.Commands;

public class RibbonCommand : ICommand
{
    public string Name => "ribbon";
    public string Description => "Click a ribbon button, optionally after switching to a tab";
    public string Usage => "ribbon <button-name> [tab-name]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length >= 2)
        {
            var tabName = args[^1];
            var searchName = string.Join(" ", args.Take(args.Length - 1));

            var tab = FindFirstEnabledVisible(window, tabName);
            if (tab == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", $"tab '{tabName}'", null, CoreSettings.GlobalOptions));
                return 1;
            }

            var before = OutputFormatter.CaptureState(window);
            TryClick(tab, tabName);
            await Task.Delay(300, ct);

            var button = FindFirstEnabledVisible(window, searchName);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", searchName, null, CoreSettings.GlobalOptions));
                return 1;
            }
            TryClick(button, searchName);

            var after = OutputFormatter.CaptureState(window);
            var diff = OutputFormatter.ComputeDiff(before, after);

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var result = new CommandResult
            {
                Command = "ribbon",
                Success = true,
                Diff = diff,
                Data = CoreSettings.Verbosity == "minimal" ? null : new { button = searchName, tab = tabName },
                DurationMs = elapsed
            };
            if (CoreSettings.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(window);
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        }
        else
        {
            var name = args[0];
            var button = FindFirstEnabledVisible(window, name);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
                return 1;
            }

            var before = OutputFormatter.CaptureState(window);
            TryClick(button, name);
            var after = OutputFormatter.CaptureState(window);
            var diff = OutputFormatter.ComputeDiff(before, after);

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var result = new CommandResult
            {
                Command = "ribbon",
                Success = true,
                Diff = diff,
                Data = CoreSettings.Verbosity == "minimal" ? null : new { button = name },
                DurationMs = elapsed
            };
            if (CoreSettings.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(window);
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        }
        return 0;
    }
}
