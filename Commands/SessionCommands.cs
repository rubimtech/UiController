using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class SessionBeginCommand : ICommand
{
    public string Name => "session-begin";
    public string Description => "Start a stateful session, optionally setting initial dialog/tab context";
    public string Usage => "session-begin [--dialog <title>] [--tab <tab>]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        SessionContext.Begin();

        string? initialDialog = null;
        string? initialTab = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dialog" && i + 1 < args.Length)
                initialDialog = args[++i];
            else if (args[i] == "--tab" && i + 1 < args.Length)
                initialTab = args[++i];
        }

        if (initialDialog != null)
            SessionContext.PushDialog(initialDialog);
        if (initialTab != null)
            SessionContext.ActiveTab = initialTab;

        var result = new CommandResult
        {
            Command = "session-begin",
            Success = true,
            Data = SessionContext.Status()
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class SessionEndCommand : ICommand
{
    public string Name => "session-end";
    public string Description => "End the current stateful session and clear all context";
    public string Usage => "session-end";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var snapshot = SessionContext.Status();
        SessionContext.End();

        var result = new CommandResult
        {
            Command = "session-end",
            Success = true,
            Data = snapshot
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class SessionStatusCommand : ICommand
{
    public string Name => "session-status";
    public string Description => "Show current session state (dialog, tab, variables, breadcrumbs)";
    public string Usage => "session-status";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var result = new CommandResult
        {
            Command = "session-status",
            Success = true,
            Data = SessionContext.Status()
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }
}
