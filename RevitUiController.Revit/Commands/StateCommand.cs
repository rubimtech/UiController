using UiController.Core;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace RevitUiController.Revit.Commands;

public class StateCommand : ICommand
{
    public string Name => "state";
    public string Description => "Quick lightweight snapshot of Revit UI state";
    public string Usage => "state";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var state = OutputFormatter.CaptureState(window);
        var result = new CommandResult
        {
            Command = "state",
            Success = true,
            Data = state
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}
