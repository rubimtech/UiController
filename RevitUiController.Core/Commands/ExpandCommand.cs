using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using static RevitUiController.Core.AutomationHelper;

namespace RevitUiController.Core.Commands;

public class ExpandCommand : ICommand
{
    public string Name => "expand";
    public string Description => "Expand/collapse details buttons in active dialogs";
    public string Usage => "expand";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var dialogs = FindActiveDialogs(window);
        if (dialogs.Count == 0)
        {
            var result = new CommandResult
            {
                Command = "expand",
                Success = true,
                Data = new { dialogsScanned = 0, buttonsClicked = 0 }
            };
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return 0;
        }

        var before = OutputFormatter.CaptureState(window);
        var clicked = 0;
        foreach (var dialog in dialogs)
        {
            var buttons = FindControlsByName(dialog, "Подробности", maxResults: 20);
            var more = FindControlsByName(dialog, "Details", maxResults: 20);
            buttons.AddRange(more.Where(b => !buttons.Any(x => x.Name == b.Name)));

            foreach (var b in buttons)
            {
                if (b.IsEnabled && TryClick(b, b.Name))
                {
                    clicked++;
                    await Task.Delay(500, ct);
                }
            }
        }
        var after = OutputFormatter.CaptureState(window);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var result2 = new CommandResult
        {
            Command = "expand",
            Success = true,
            Diff = diff,
            Data = new { dialogsScanned = dialogs.Count, buttonsClicked = clicked }
        };
        Console.Write(OutputFormatter.FormatResult(result2, CoreSettings.GlobalOptions));
        return 0;
    }
}
