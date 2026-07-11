using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class SafeClickCommand : ICommand
{
    public string Name => "safe-click";
    public string Description => "Idempotent click: click if element exists, succeed if already gone";
    public string Usage => "safe-click <name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("SafeClickCommand", "Usage: safe-click <name>");
            return Task.FromResult(1);
        }
        
        var name = string.Join(" ", args);
        var before = OutputFormatter.CaptureState(window);
        
        var element = AutomationHelper.FindFirstEnabledVisible(window, name);
        if (element == null)
        {
            var after = OutputFormatter.CaptureState(window);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "safe-click",
                Success = true,
                Data = new { action = "skipped", reason = "element_not_found" },
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        
        try
        {
            element.Click();
            var after = OutputFormatter.CaptureState(window);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "safe-click",
                Success = true,
                Data = new { action = "clicked", target = name },
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            var after = OutputFormatter.CaptureState(window);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "safe-click",
                Success = false,
                Error = ex.Message,
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
