using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;
namespace RevitUiController.Commands;

public class SafetyCheckCommand : ICommand
{
    public string Name => "safety-check";
    public string Description => "Check for unexpected warning dialogs";
    public string Usage => "safety-check";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (Program.EventService is { IsListening: true })
        {
            var unexpected = SafetyGuard.FindUnexpectedDialogs(revitWindow);
            if (unexpected.Count > 0)
            {
                await SafetyGuard.DismissWarningDialogs(revitWindow, ct);
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                EventHandler<AutomationEventService.UiEventRecord>? handler = null;
                handler = (_, record) =>
                {
                    if (record.Type == "WindowOpened" && record.ControlType == "Window")
                    {
                        var dialogs = SafetyGuard.FindUnexpectedDialogs(revitWindow);
                        if (dialogs.Count > 0)
                        {
                            tcs.TrySetResult(true);
                            if (Program.EventService != null)
                                Program.EventService.OnEvent -= handler;
                        }
                    }
                };
                if (Program.EventService != null)
                {
                    Program.EventService.OnEvent += handler;
                    using var ctr = ct.Register(() => { tcs.TrySetResult(false); if (Program.EventService != null) Program.EventService.OnEvent -= handler; });
                    if (await Task.WhenAny(tcs.Task, Task.Delay(5000, ct)) == tcs.Task && tcs.Task.Result)
                    {
                        await SafetyGuard.DismissWarningDialogs(revitWindow, ct);
                    }
                    Program.EventService.OnEvent -= handler;
                }
            }
        }
        else
        {
            var unexpected = SafetyGuard.FindUnexpectedDialogs(revitWindow);
            await SafetyGuard.DismissWarningDialogs(revitWindow, ct);
        }

        var found = SafetyGuard.FindUnexpectedDialogs(revitWindow);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "safety-check",
            Success = found.Count == 0,
            Error = found.Count > 0 ? $"Found {found.Count} unexpected dialog(s) after dismiss" : null,
            Data = new { remainingDialogs = found.Count, dismissed = true }
        }, Program.GlobalOptions));
        return found.Count == 0 ? 0 : 1;
    }
}

public class RevitRestartCommand : ICommand
{
    public string Name => "revit-restart";
    public string Description => "Check Revit process health and restart if needed";
    public string Usage => "revit-restart [--path <exe-path>] [--pid <number>] [--process-name <name>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
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
            }, Program.GlobalOptions));
            return 0;
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
            }, Program.GlobalOptions));
            return 1;
        }

        var ready = await SafetyGuard.WaitForRevitReady(120000, ct);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "revit-restart",
            Success = ready,
            Error = ready ? null : "Revit started but did not become ready within 120s",
            Data = new { action = "started", pid = started.Id, ready }
        }, Program.GlobalOptions));
        return ready ? 0 : 1;
    }
}
