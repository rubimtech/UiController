using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class ScrollToCommand : ICommand
{
    public string Name => "scroll-to";
    public string Description => "Scroll to bring an element into view (ScrollIntoView). Usage: scroll-to <name> [--parent <p>]";
    public string Usage => "scroll-to <name> [--parent <p>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: scroll-to <name> [--parent <p>]");
            return 1;
        }

        string? parentFilter = null;
        var name = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--parent" && i + 1 < args.Length) parentFilter = args[++i];
            else name = (name == "" ? args[i] : name + " " + args[i]);
        }

        AutomationElement? scope = revitWindow;
        if (parentFilter != null)
        {
            scope = AutomationHelper.FindFirstEnabledVisible(revitWindow, parentFilter);
            if (scope == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", parentFilter, ["parent"], Program.GlobalOptions));
                return 1;
            }
        }

        var element = AutomationHelper.FindFirstEnabledVisible(scope, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.GlobalOptions));
            return 1;
        }

        var success = false;
        try
        {
            var scrollItem = element.Patterns.ScrollItem.Pattern;
            if (scrollItem != null)
            {
                scrollItem.ScrollIntoView();
                success = true;
                await Task.Delay(200, ct);
            }
            else
            {
                Console.Error.WriteLine("Element does not support ScrollItemPattern, trying parent...");
                var parent = AutomationHelper.FindParent(element);
                if (parent != null)
                {
                    var parentScrollItem = parent.Patterns.ScrollItem.Pattern;
                    if (parentScrollItem != null)
                    {
                        parentScrollItem.ScrollIntoView();
                        success = true;
                        await Task.Delay(200, ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ScrollIntoView failed: {ex.Message}");
        }

        var result = new CommandResult
        {
            Command = "scroll-to",
            Success = success,
            Error = success ? null : "ScrollIntoView not supported or failed",
            Data = new
            {
                target = name,
                parent = parentFilter,
                scrolled = success,
                bounds = success ? new
                {
                    x = element.BoundingRectangle.X,
                    y = element.BoundingRectangle.Y,
                    w = element.BoundingRectangle.Width,
                    h = element.BoundingRectangle.Height
                } : null
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return success ? 0 : 1;
    }
}
