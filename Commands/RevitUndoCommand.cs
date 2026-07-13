using System.Text.Json;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class RevitUndoCommand : ICommand
{
    public string Name => "revit-undo";
    public string Description => "Query undo stack or perform programmatic undo via Revit API pipe";
    public string Usage => "revit-undo [action] [--count N]\n  Actions: status (default), undo (perform N undo steps)";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var action = "status";
        var count = 1;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--count" && i + 1 < args.Length && int.TryParse(args[++i], out var c))
                count = Math.Max(1, c);
            else if (args[i] is "status" or "undo" or "rollback")
                action = args[i];
        }

        using var client = new PipeBridgeClient();
        if (!client.Connect(2000))
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-undo",
                Success = false,
                Error = "Failed to connect to ReVibe Named Pipe. Is Revit running with the plugin loaded?"
            }, Program.GlobalOptions));
            return 1;
        }

        switch (action)
        {
            case "status":
            {
                var response = client.SendCommand("undo_stack", new { }, 5000);
                UndoStackInfo? stackInfo = null;
                if (response != null)
                {
                    try { stackInfo = JsonSerializer.Deserialize<UndoStackInfo>(response); } catch { }
                }
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "revit-undo",
                    Success = stackInfo != null,
                    Error = stackInfo == null ? "Undo stack query returned null" : null,
                    Data = stackInfo
                }, Program.GlobalOptions));
                return stackInfo != null ? 0 : 1;
            }

            case "undo":
            case "rollback":
            {
                var response = client.SendCommand("undo", new { count }, 5000);
                var success = response != null;
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "revit-undo",
                    Success = success,
                    Error = success ? null : "Undo command failed",
                    Data = new { action = "undo", count, success }
                }, Program.GlobalOptions));
                return success ? 0 : 1;
            }

            default:
                Console.Write(OutputFormatter.FormatError(UiController.Core.Models.ErrorCode.InvalidArgs, 
                    $"Unknown action: {action}. Use 'status' or 'undo'."));
                return 1;
        }
    }
}

public class UndoStackInfo
{
    public int UndoCount { get; set; }
    public int RedoCount { get; set; }
    public List<string>? UndoItems { get; set; }
    public List<string>? RedoItems { get; set; }
    public bool CanUndo => UndoCount > 0;
}
