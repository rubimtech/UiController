using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class HighlightCommand : ICommand
{
    public string Name => "highlight";
    public string Description => "Highlight an element on screen: highlight <name> [duration-ms]";
    public string Usage => "highlight <element-name> [duration-ms]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("HighlightCommand", "Usage: highlight <element-name> [duration-ms]");
            return Task.FromResult(1);
        }

        var name = args[0];
        var duration = args.Length > 1 && int.TryParse(args[1], out var d) ? d : 1500;

        var element = FindFirstEnabledVisible(window, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        HighlightHelper.Highlight(element, duration);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "highlight",
            Success = true,
            Data = new { target = name, durationMs = duration }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class HighlightClearCommand : ICommand
{
    public string Name => "highlight-clear";
    public string Description => "Clear any active highlight overlay";
    public string Usage => "highlight-clear";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        HighlightHelper.Clear();
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "highlight-clear",
            Success = true
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}
