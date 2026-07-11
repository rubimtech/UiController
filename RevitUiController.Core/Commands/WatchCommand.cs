using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class WatchCommand : ICommand
{
    public string Name => "watch";
    public string Description => "Poll a command until a condition is met. Usage: watch <command> [args...] --interval <sec> --until <found|gone|enabled|disabled|text:substring> [--timeout <sec>]";
    public string Usage => "watch <command> [args...] --interval <sec> --until <condition> [--timeout <sec>]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2)
        {
            LoggingService.Error("WatchCommand", "Usage: watch <command> [args...] --interval <sec> --until <found|gone|enabled|disabled|text:substring> [--timeout <sec>]");
            return 1;
        }

        int interval = 2;
        int timeout = 60;
        string? condition = null;
        var cmdArgs = new List<string>();
        int i = 0;

        for (; i < args.Length; i++)
        {
            if (args[i] == "--interval" && i + 1 < args.Length) { int.TryParse(args[++i], out interval); }
            else if (args[i] == "--timeout" && i + 1 < args.Length) { int.TryParse(args[++i], out timeout); }
            else if (args[i] == "--until" && i + 1 < args.Length) { condition = args[++i]; }
            else { cmdArgs.Add(args[i]); }
        }

        if (string.IsNullOrEmpty(condition) || cmdArgs.Count == 0)
        {
            LoggingService.Error("WatchCommand", "Usage: watch <command> [args...] --interval <sec> --until <condition> [--timeout <sec>]");
            return 1;
        }

        var cmdName = cmdArgs[0].ToLowerInvariant();
        var cmd = ProgramHelper.GetCommand(cmdName);
        if (cmd == null)
        {
            LoggingService.Error("WatchCommand", $"Unknown command: {cmdName}");
            return 1;
        }

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        var pollArgs = cmdArgs.Skip(1).ToArray();
        var deadlineStr = $"timeout={timeout}s";

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var exitCode = await cmd.ExecuteAsync(window, pollArgs);
                if (EvaluateCondition(exitCode == 0, condition))
                {
                    Console.Write(OutputFormatter.FormatResult(new CommandResult
                    {
                        Command = "watch", Success = true,
                        Data = new { command = cmdName, condition, matched = true, deadlineStr }
                    }, CoreSettings.GlobalOptions));
                    return 0;
                }
            }
            catch { }

            await Task.Delay(interval * 1000);
        }

        Console.Write(OutputFormatter.FormatError("Timeout", $"watch {cmdName}", [$"condition '{condition}' not met within {timeout}s"], CoreSettings.GlobalOptions));
        return 1;
    }

    private static bool EvaluateCondition(bool found, string condition)
    {
        if (condition == "found") return found;
        if (condition == "gone") return !found;
        if (condition == "enabled") return found;
        if (condition == "disabled") return !found;
        if (condition.StartsWith("text:"))
        {
            var sub = condition[5..];
            return OutputFormatter.LastOutput?.Contains(sub, StringComparison.OrdinalIgnoreCase) == true;
        }
        return found;
    }
}

public static class ProgramHelper
{
    public static ICommand? GetCommand(string name)
    {
        return CoreSettings.CommandRegistry?.GetCommand(name);
    }
}
