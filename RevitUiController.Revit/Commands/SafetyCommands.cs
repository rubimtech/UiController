using UiController.Core;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;
namespace RevitUiController.Revit.Commands;

public class SafetyCheckCommand : ICommand
{
    public string Name => "safety-check";
    public string Description => "Check for unexpected warning dialogs";
    public string Usage => "safety-check";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (CoreSettings.EventService is { IsListening: true })
        {
            var unexpected = SafetyGuard.FindUnexpectedDialogs(window);
            if (unexpected.Count > 0)
            {
                await SafetyGuard.DismissWarningDialogs(window, ct);
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                EventHandler<AutomationEventService.UiEventRecord>? handler = null;
                handler = (_, record) =>
                {
                    if (record.Type == "WindowOpened" && record.ControlType == "Window")
                    {
                        var dialogs = SafetyGuard.FindUnexpectedDialogs(window);
                        if (dialogs.Count > 0)
                        {
                            tcs.TrySetResult(true);
                            if (CoreSettings.EventService != null)
                                CoreSettings.EventService.OnEvent -= handler;
                        }
                    }
                };
                if (CoreSettings.EventService != null)
                {
                    CoreSettings.EventService.OnEvent += handler;
                    using var ctr = ct.Register(() => { tcs.TrySetResult(false); if (CoreSettings.EventService != null) CoreSettings.EventService.OnEvent -= handler; });
                    if (await Task.WhenAny(tcs.Task, Task.Delay(5000, ct)) == tcs.Task && tcs.Task.Result)
                    {
                        await SafetyGuard.DismissWarningDialogs(window, ct);
                    }
                    CoreSettings.EventService.OnEvent -= handler;
                }
            }
        }
        else
        {
            var unexpected = SafetyGuard.FindUnexpectedDialogs(window);
            await SafetyGuard.DismissWarningDialogs(window, ct);
        }

        var found = SafetyGuard.FindUnexpectedDialogs(window);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "safety-check",
            Success = found.Count == 0,
            Error = found.Count > 0 ? $"Found {found.Count} unexpected dialog(s) after dismiss" : null,
            Data = new { remainingDialogs = found.Count, dismissed = true }
        }, CoreSettings.GlobalOptions));
        return found.Count == 0 ? 0 : 1;
    }
}

public class RevitRestartCommand : ICommand
{
    public string Name => "revit-restart";
    public string Description => "Check Revit process health and restart if needed";
    public string Usage => "revit-restart [--path <exe-path>] [--pid <number>] [--process-name <name>]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var process = RevitSafetyExtensions.GetRevitProcess();
        var isAlive = RevitSafetyExtensions.IsRevitProcessAlive(process);
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
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        Console.WriteLine("Revit is not running. Starting...");
        var started = RevitSafetyExtensions.StartRevit(path);

        if (started == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-restart",
                Success = false,
                Error = "Failed to start Revit process"
            }, CoreSettings.GlobalOptions));
            return 1;
        }

        var ready = await RevitSafetyExtensions.WaitForRevitReady(120000, ct);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "revit-restart",
            Success = ready,
            Error = ready ? null : "Revit started but did not become ready within 120s",
            Data = new { action = "started", pid = started.Id, ready }
        }, CoreSettings.GlobalOptions));
        return ready ? 0 : 1;
    }
}
