using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

namespace RevitUiController.Commands;

public class WaitCommand : ICommand
{
    public string Name => "wait";
    public string Description => "Wait N seconds";
    public string Usage => "wait <seconds>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var sec))
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "wait <seconds>", null, Program.IsPretty));
            return Task.FromResult(1);
        }

        Thread.Sleep(sec * 1000);

        var result = new CommandResult
        {
            Command = "wait",
            Success = true,
            Data = new { seconds = sec }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class ScriptCommand : ICommand
{
    private readonly Dictionary<string, ICommand> _commands;

    public string Name => "script";
    public string Description => "Run commands from a script file (in-process)";
    public string Usage => "script <file-path>";

    public ScriptCommand(Dictionary<string, ICommand> commands)
    {
        _commands = commands;
    }

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "script <file-path>", null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var filePath = string.Join(" ", args);
        if (!File.Exists(filePath))
        {
            Console.Write(OutputFormatter.FormatError("FileNotFound", filePath, null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var lines = File.ReadAllLines(filePath);
        var executed = 0;
        var failed = 0;
        CommandResult? lastResult = null;

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var tokens = Tokenize(trimmed);
            if (tokens.Length == 0) continue;

            var cmdName = tokens[0].ToLowerInvariant();
            var cmdArgs = tokens.Skip(1).ToArray();

            if (cmdName == "window" && cmdArgs.Length > 0)
            {
                var title = string.Join(" ", cmdArgs);
                Console.WriteLine($"  [scope set to: {title}]");
                if (SessionContext.IsActive)
                    SessionContext.PushDialog(title);
                continue;
            }

            if (cmdName == "set" && cmdArgs.Length >= 2)
            {
                var varName = cmdArgs[0];
                var varValue = string.Join(" ", cmdArgs.Skip(1));
                SessionContext.SetVariable(varName, varValue);
                Console.WriteLine($"  [set ${varName} = \"{varValue}\"]");
                continue;
            }

            if (cmdName == "get-output" && cmdArgs.Length >= 1)
            {
                var varName = cmdArgs[0];
                SessionContext.SetVariable($"{varName}.success", lastResult?.Success ?? false);
                if (lastResult?.Data != null)
                    SessionContext.SetVariable(varName, lastResult.Data);
                Console.WriteLine($"  [get-output ${varName} = {lastResult?.Data ?? "null"}]");
                continue;
            }

            cmdArgs = ExpandVariables(cmdArgs);

            if (SessionContext.IsActive)
                cmdArgs = ApplySessionScope(cmdName, cmdArgs);

            if (cmdName == "wait-for" && cmdArgs.Length > 0)
            {
                var title = cmdArgs[0];
                var timeout = cmdArgs.Length > 1 && int.TryParse(cmdArgs[1], out var t) ? t * 1000 : 15000;
                Console.WriteLine($"  Waiting for dialog '{title}' (timeout: {timeout / 1000}s)...");
                var dialog = Retry.WaitForDialog(revitWindow, title, timeout);
                if (dialog != null)
                {
                    Console.WriteLine($"  Dialog appeared: \"{dialog.Name}\"");
                    if (SessionContext.IsActive)
                        SessionContext.PushDialog(dialog.Name ?? title);
                }
                else
                {
                    Console.Error.WriteLine($"  Dialog '{title}' did not appear within timeout");
                }
                continue;
            }

            if (cmdName == "wait-close" && cmdArgs.Length > 0)
            {
                var title = cmdArgs[0];
                var timeout = cmdArgs.Length > 1 && int.TryParse(cmdArgs[1], out var t) ? t * 1000 : 15000;
                Console.WriteLine($"  Waiting for dialog '{title}' to close...");
                var closed = Retry.WaitForDialogClose(revitWindow, title, timeout);
                if (closed)
                {
                    Console.WriteLine($"  Dialog closed: \"{title}\"");
                    if (SessionContext.IsActive)
                        SessionContext.PopDialog();
                }
                else
                {
                    Console.Error.WriteLine($"  Dialog '{title}' did not close within timeout");
                }
                continue;
            }

            if (cmdName == "select" && cmdArgs.Length >= 2)
            {
                var label = cmdArgs[0];
                var option = string.Join(" ", cmdArgs.Skip(1));
                AutomationElement? scope = revitWindow;

                if (SessionContext.IsActive && !string.IsNullOrEmpty(SessionContext.ActiveDialog))
                {
                    var dialogs = FindActiveDialogs(revitWindow);
                    scope = dialogs.FirstOrDefault(d =>
                        (d.Name ?? "").Contains(SessionContext.ActiveDialog, StringComparison.OrdinalIgnoreCase));
                    if (scope == null) scope = revitWindow;
                }

                Console.WriteLine($"  Selecting '{option}' in '{label}'...");
                var combo = FindFirstEnabledVisible(scope, label);
                if (combo != null)
                {
                    combo.Click();
                    Thread.Sleep(300);
                    foreach (var c in SafeGetChildren(combo, 3000))
                    {
                        try
                        {
                            if ((c.Name ?? "").Contains(option, StringComparison.OrdinalIgnoreCase))
                            { c.Click(); Console.WriteLine($"  Selected: {option}"); break; }
                        }
                        catch { }
                    }
                }
                else
                {
                    Console.Error.WriteLine($"  ComboBox '{label}' not found");
                }
                continue;
            }

            if (cmdName == "click" || cmdName == "safe-click" || cmdName == "ribbon")
            {
                var fullDesc = $"{cmdName} {string.Join(" ", cmdArgs)}";
                if (SafetyGuard.IsDestructive(cmdName, cmdArgs))
                {
                    if (!SafetyGuard.ConfirmDestructiveAction(fullDesc))
                    {
                        Console.Error.WriteLine($"  [SAFETY] Blocked: {fullDesc}");
                        continue;
                    }
                    Console.WriteLine($"  [SAFETY] Confirmed: {fullDesc}");
                }
            }

            if (!_commands.TryGetValue(cmdName, out var cmd))
            {
                Console.Error.WriteLine($"  Unknown command '{cmdName}' in script, skipping.");
                continue;
            }

            executed++;
            var exitCode = cmd.ExecuteAsync(revitWindow, cmdArgs).GetAwaiter().GetResult();
            if (exitCode != 0)
                failed++;
            LoggingService.Info("script", $"Executed: {cmdName} {string.Join(" ", cmdArgs)}");
            RecorderService.Record($"{cmdName} {string.Join(" ", cmdArgs)}");
        }

        var result = new CommandResult
        {
            Command = "script",
            Success = failed == 0,
            Data = new { file = filePath, totalLines = lines.Length, executed, failed }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }

    private static string[] ExpandVariables(string[] args)
    {
        return args.Select(ExpandVariable).ToArray();
    }

    private static string ExpandVariable(string arg)
    {
        return Regex.Replace(arg, @"\$(\w+(?:\.\w+)*)", match =>
        {
            var name = match.Groups[1].Value;
            var value = SessionContext.GetVariable(name);
            return value?.ToString() ?? match.Value;
        });
    }

    private static readonly HashSet<string> DialogFirstCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "ps", "taskdialog"
    };

    private static string[] ApplySessionScope(string cmdName, string[] args)
    {
        if (!SessionContext.IsActive || string.IsNullOrEmpty(SessionContext.ActiveDialog))
            return args;

        if (DialogFirstCommands.Contains(cmdName) && (args.Length == 0 ||
            !args[0].Contains(SessionContext.ActiveDialog, StringComparison.OrdinalIgnoreCase)))
        {
            var newArgs = new string[args.Length + 1];
            newArgs[0] = SessionContext.ActiveDialog;
            Array.Copy(args, 0, newArgs, 1, args.Length);
            return newArgs;
        }

        return args;
    }
}
