using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class SafetyCheckCommand : ICommand
{
    public string Name => "safety-check";
    public string Description => "Check for unexpected warning dialogs";
    public string Usage => "safety-check";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var unexpected = SafetyGuard.FindUnexpectedDialogs(revitWindow);
        SafetyGuard.DismissWarningDialogs(revitWindow);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "safety-check",
            Success = unexpected.Count == 0,
            Error = unexpected.Count > 0 ? $"Dismissed {unexpected.Count} unexpected dialog(s)" : null,
            Data = new { unexpectedDialogsFound = unexpected.Count, dismissed = true }
        }, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class RevitRestartCommand : ICommand
{
    public string Name => "revit-restart";
    public string Description => "Check Revit process health and restart if needed";
    public string Usage => "revit-restart [--path <exe-path>] [--pid <number>] [--process-name <name>]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var process = SafetyGuard.GetRevitProcess();
        var isAlive = SafetyGuard.IsRevitProcessAlive(process);
        string? path = null;
        int? pid = null;
        string? processName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--path" && i + 1 < args.Length)
                path = args[++i];
            else if (args[i] == "--pid" && i + 1 < args.Length && int.TryParse(args[++i], out var p))
                pid = p;
            else if (args[i] == "--process-name" && i + 1 < args.Length)
                processName = args[++i];
            else if (path == null && !args[i].StartsWith("--"))
                path = args[i];
        }

        if (isAlive)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-restart",
                Success = true,
                Data = new { action = "none", reason = "already_running", pid = process?.Id }
            }, Program.IsPretty));
            return Task.FromResult(0);
        }

        Console.WriteLine("Revit is not running. Starting...");
        var started = SafetyGuard.StartRevit(path);

        if (started == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-restart",
                Success = false,
                Error = "Failed to start Revit process"
            }, Program.IsPretty));
            return Task.FromResult(1);
        }

        var ready = SafetyGuard.WaitForRevitReady(120000);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "revit-restart",
            Success = ready,
            Error = ready ? null : "Revit started but did not become ready within 120s",
            Data = new { action = "started", pid = started.Id, ready }
        }, Program.IsPretty));
        return Task.FromResult(ready ? 0 : 1);
    }
}
