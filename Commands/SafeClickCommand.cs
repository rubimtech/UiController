using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class SafeClickCommand : ICommand
{
    public string Name => "safe-click";
    public string Description => "Idempotent click: click if element exists, succeed if already gone";
    public string Usage => "safe-click <name>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: safe-click <name>");
            return Task.FromResult(1);
        }
        
        var name = string.Join(" ", args);
        var before = OutputFormatter.CaptureState(revitWindow);
        
        var element = AutomationHelper.FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            var after = OutputFormatter.CaptureState(revitWindow);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "safe-click",
                Success = true,
                Data = new { action = "skipped", reason = "element_not_found" },
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, Program.GlobalOptions));
            return Task.FromResult(0);
        }
        
        try
        {
            element.Click();
            var after = OutputFormatter.CaptureState(revitWindow);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "safe-click",
                Success = true,
                Data = new { action = "clicked", target = name },
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, Program.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            var after = OutputFormatter.CaptureState(revitWindow);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "safe-click",
                Success = false,
                Error = ex.Message,
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, Program.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
