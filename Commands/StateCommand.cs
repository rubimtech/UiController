using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class StateCommand : ICommand
{
    public string Name => "state";
    public string Description => "Quick lightweight snapshot of Revit UI state";
    public string Usage => "state";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var state = OutputFormatter.CaptureState(revitWindow);
        var result = new CommandResult
        {
            Command = "state",
            Success = true,
            Data = state
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }
}
