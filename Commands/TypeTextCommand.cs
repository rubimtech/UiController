using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Commands;

public class TypeTextCommand : ICommand
{
    public string Name => "type";
    public string Description => "Type text into a control";
    public string Usage => "type <control-name> <text>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "type <control-name> <text>", null, Program.GlobalOptions));
            return 1;
        }

        var controlName = args[0];
        var text = string.Join(" ", args.Skip(1));

        var found = FindFirstEnabledVisible(revitWindow, controlName);
        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", controlName, null, Program.GlobalOptions));
            return 1;
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        found.Focus();
        found.Click();
        await Task.Delay(200, ct);
        SendTextSafe(found, text);
        var after = OutputFormatter.CaptureState(revitWindow);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var result = new CommandResult
        {
            Command = "type",
            Success = true,
            Diff = diff,
            Data = Program.Verbosity == "minimal"
                ? new { control = controlName }
                : new { control = controlName, text, targetName = found.Name ?? "" }
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return 0;
    }
}
