using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class WatchCommand : ICommand
{
    public string Name => "watch";
    public string Description => "Poll a command until a condition is met. Usage: watch <command> [args...] --interval <sec> --until <found|gone|enabled|disabled|text:substring> [--timeout <sec>]";
    public string Usage => "watch <command> [args...] --interval <sec> --until <condition> [--timeout <sec>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: watch <command> [args...] --interval <sec> --until <found|gone|enabled|disabled|text:substring> [--timeout <sec>]");
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
            Console.Error.WriteLine("Usage: watch <command> [args...] --interval <sec> --until <condition> [--timeout <sec>]");
            return 1;
        }

        var cmdName = cmdArgs[0].ToLowerInvariant();
        var cmd = ProgramHelper.GetCommand(cmdName);
        if (cmd == null)
        {
            Console.Error.WriteLine($"Unknown command: {cmdName}");
            return 1;
        }

        var deadline = DateTime.UtcNow.AddSeconds(timeout);
        var pollArgs = cmdArgs.Skip(1).ToArray();
        var deadlineStr = $"timeout={timeout}s";

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var exitCode = await cmd.ExecuteAsync(revitWindow, pollArgs);
                if (EvaluateCondition(exitCode == 0, condition))
                {
                    Console.Write(OutputFormatter.FormatResult(new CommandResult
                    {
                        Command = "watch", Success = true,
                        Data = new { command = cmdName, condition, matched = true, deadlineStr }
                    }, Program.IsPretty));
                    return 0;
                }
            }
            catch { }

            await Task.Delay(interval * 1000);
        }

        Console.Write(OutputFormatter.FormatError("Timeout", $"watch {cmdName}", [$"condition '{condition}' not met within {timeout}s"], Program.IsPretty));
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
            return Program.LastOutput?.Contains(sub, StringComparison.OrdinalIgnoreCase) == true;
        }
        return found;
    }
}

public static class ProgramHelper
{
    public static ICommand? GetCommand(string name)
    {
        var commandsField = typeof(Program).GetField("Commands", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        if (commandsField?.GetValue(null) is Dictionary<string, ICommand> cmds)
        {
            if (cmds.TryGetValue(name, out var cmd)) return cmd;
        }
        return null;
    }
}
