using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;
namespace UiController.Core.Commands;

public class WaitForCommand : ICommand
{
    public string Name => "wait-for";
    public string Description => "Wait for a dialog to appear: wait-for <title> [timeout]";
    public string Usage => "wait-for <title> [timeout]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("WaitForCommand", "Usage: wait-for <title> [timeout]");
            return 1;
        }
        
        var title = args[0];
        var timeout = args.Length > 1 && int.TryParse(args[1], out var t) ? t * 1000 : 15000;
        
        AutomationElement? dialog;
        
        if (CoreSettings.EventService is { IsListening: true })
        {
            dialog = await CoreSettings.EventService.WaitForDialogAsync(title, timeout, ct);
        }
        else
        {
            dialog = await Retry.WaitForDialog(window, title, timeout, ct: ct);
        }
        
        if (dialog == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wait-for",
                Success = false,
                Error = $"Dialog '{title}' did not appear within {timeout / 1000}s"
            }, CoreSettings.GlobalOptions));
            return 1;
        }
        
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wait-for",
            Success = true,
            Data = new { title = dialog.Name ?? title }
        }, CoreSettings.GlobalOptions));
        return 0;
    }
}

public class WaitCloseCommand : ICommand
{
    public string Name => "wait-close";
    public string Description => "Wait for a dialog to close: wait-close <title> [timeout]";
    public string Usage => "wait-close <title> [timeout]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("WaitForCommand", "Usage: wait-close <title> [timeout]");
            return 1;
        }
        
        var title = args[0];
        var timeout = args.Length > 1 && int.TryParse(args[1], out var t) ? t * 1000 : 15000;
        
        bool closed;
        
        if (CoreSettings.EventService is { IsListening: true })
        {
            closed = await CoreSettings.EventService.WaitForDialogCloseAsync(title, timeout, ct);
        }
        else
        {
            closed = await Retry.WaitForDialogClose(window, title, timeout, ct: ct);
        }
        
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wait-close",
            Success = closed,
            Error = closed ? null : $"Dialog '{title}' did not close within {timeout / 1000}s",
            Data = new { title }
        }, CoreSettings.GlobalOptions));
        return closed ? 0 : 1;
    }
}

public class WaitForElementCommand : ICommand
{
    public string Name => "wait-element";
    public string Description => "Wait for a UI element to appear: wait-element <name> [timeout]";
    public string Usage => "wait-element <name> [timeout]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("WaitForCommand", "Usage: wait-element <name> [timeout]");
            return 1;
        }
        
        var name = string.Join(" ", args.Where(a => !a.StartsWith("--")));
        var timeoutArg = args.FirstOrDefault(a => int.TryParse(a, out _));
        var timeout = timeoutArg != null ? int.Parse(timeoutArg) * 1000 : 10000;
        
        AutomationElement? element;
        
        if (CoreSettings.EventService is { IsListening: true })
        {
            element = await CoreSettings.EventService.WaitForElementAsync(name, timeout, ct);
        }
        else
        {
            element = await Retry.WaitForElement(window, name, timeout, ct: ct);
        }
        
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wait-element",
                Success = false,
                Error = $"Element '{name}' did not appear within {timeout / 1000}s"
            }, CoreSettings.GlobalOptions));
            return 1;
        }
        
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wait-element",
            Success = true,
            Data = OutputFormatter.FromAutomationElement(element, 0, 1)
        }, CoreSettings.GlobalOptions));
        return 0;
    }
}
