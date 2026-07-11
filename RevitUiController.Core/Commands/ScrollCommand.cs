using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class ScrollCommand : ICommand
{
    public string Name => "scroll";
    public string Description => "Scroll a container. Usage: scroll [--horizontal amount] [--vertical amount] [--target name]";
    public string Usage => "scroll [--horizontal amount] [--vertical amount] [--target name]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        double? horizontalAmount = null;
        double? verticalAmount = null;
        string? targetName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--horizontal" && i + 1 < args.Length && double.TryParse(args[++i], out var h))
                horizontalAmount = h;
            else if (args[i] == "--vertical" && i + 1 < args.Length && double.TryParse(args[++i], out var v))
                verticalAmount = v;
            else if (args[i] == "--target" && i + 1 < args.Length)
                targetName = args[++i];
        }

        if (horizontalAmount == null && verticalAmount == null)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "Specify at least --horizontal or --vertical", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        AutomationElement? container = window;
        if (targetName != null)
        {
            container = FindFirstEnabledVisible(window, targetName);
            if (container == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", targetName, null, CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }
        }

        try
        {
            var scroll = container.Patterns.Scroll.Pattern;
            if (scroll == null)
            {
                Console.Write(OutputFormatter.FormatError("ScrollNotSupported", targetName ?? "window", ["ScrollPattern not available"], CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }

            double horizBefore = scroll.HorizontalScrollPercent;
            double vertBefore = scroll.VerticalScrollPercent;

            scroll.SetScrollPercent(
                horizontalAmount.HasValue ? horizontalAmount.Value : -1,
                verticalAmount.HasValue ? verticalAmount.Value : -1);

            var result = new CommandResult
            {
                Command = "scroll",
                Success = true,
                Data = new
                {
                    target = targetName ?? "window",
                    horizontal = horizontalAmount,
                    vertical = verticalAmount,
                    before = new { horizPercent = horizBefore, vertPercent = vertBefore },
                    after = new
                    {
                        horizPercent = scroll.HorizontalScrollPercent,
                        vertPercent = scroll.VerticalScrollPercent
                    }
                }
            };
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("ScrollFailed", targetName ?? "window", [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
