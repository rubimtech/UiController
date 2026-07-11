using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class TypeTextCommand : ICommand
{
    public string Name => "type";
    public string Description => "Type text into a control";
    public string Usage => "type <control-name> <text>";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "type <control-name> <text>", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var controlName = args[0];
        var text = string.Join(" ", args.Skip(1));

        var found = FindFirstEnabledVisible(window, controlName);
        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", controlName, null, CoreSettings.GlobalOptions));
            return 1;
        }

        var before = OutputFormatter.CaptureState(window);
        found.Focus();
        found.Click();
        await Task.Delay(200, ct);
        SendTextSafe(found, text);
        var after = OutputFormatter.CaptureState(window);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var result = new CommandResult
        {
            Command = "type",
            Success = true,
            Diff = diff,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { control = controlName }
                : new { control = controlName, text, targetName = found.Name ?? "" }
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }
}
