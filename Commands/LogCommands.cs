using RevitUiController.Models;
using System.Threading;
using System.Threading;

namespace RevitUiController.Commands;

public class LogsCommand : ICommand
{
    public string Name => "logs";
    public string Description => "Read RevitUiController or revitCopilot logs";
    public string Usage => "logs [--tail N] [--since HH:mm] [--level Error] [--plugin]";

    public Task<int> ExecuteAsync(FlaUI.Core.AutomationElements.AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        int tail = 50;
        string? level = null;
        DateTime? since = null;
        bool plugin = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--tail" && i + 1 < args.Length && int.TryParse(args[++i], out var t))
                tail = t;
            else if (args[i] == "--level" && i + 1 < args.Length)
                level = args[++i];
            else if (args[i] == "--since" && i + 1 < args.Length)
            {
                if (DateTime.TryParse(args[i + 1], out var dt))
                    since = dt;
                else if (TimeSpan.TryParse(args[i + 1], out var ts))
                    since = DateTime.Today.Add(ts);
                i++;
            }
            else if (args[i] == "--plugin")
                plugin = true;
        }

        string[] lines;
        if (plugin)
        {
            LoggingService.Info("logs", $"Reading plugin logs (tail={tail}, level={level})");
            lines = LoggingService.ReadPluginLogs(tail, level);
        }
        else
        {
            LoggingService.Info("logs", $"Reading controller logs (tail={tail}, level={level})");
            lines = LoggingService.ReadLogs(tail, level, since);
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "logs",
            Success = true,
            Data = new { source = plugin ? "plugin" : "controller", total = lines.Length, lines }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class DryRunCommand : ICommand
{
    public string Name => "dry-run";
    public string Description => "Execute a .rvs script in dry-run mode (show steps, no actual clicks)";
    public string Usage => "dry-run <script-path>";

    public Task<int> ExecuteAsync(FlaUI.Core.AutomationElements.AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "dry-run <script-path>", null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var filePath = string.Join(" ", args);
        if (!File.Exists(filePath))
        {
            Console.Write(OutputFormatter.FormatError("FileNotFound", filePath, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var lines = File.ReadAllLines(filePath);
        var steps = new List<object>();

        foreach (var rawLine in lines)
        {
            var trimmed = rawLine.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

            var tokens = AutomationHelper.Tokenize(trimmed);
            if (tokens.Length == 0) continue;

            steps.Add(new
            {
                command = tokens[0],
                args = string.Join(" ", tokens.Skip(1)),
                action = GetDryRunDescription(tokens[0])
            });
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "dry-run",
            Success = true,
            Data = new { file = filePath, totalSteps = steps.Count, steps }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }

    private static string GetDryRunDescription(string cmd)
    {
        return cmd switch
        {
            "click" or "safe-click" => "would click button",
            "ribbon" => "would click ribbon button",
            "type" => "would type text",
            "wait" => "would wait (NOP in dry-run)",
            "wait-for" => "would wait for dialog (NOP)",
            "wait-close" => "would wait for dialog close (NOP)",
            "switch-view" or "sv" => "would switch view",
            "select" => "would select combo item",
            "expand" => "would expand details",
            "script" => "would run sub-script",
            "assert-dialog" or "assert-ribbon" or "assert-view" => "would assert (NOP in dry-run)",
            "mouse-click" or "mouse-drag" or "mouse-scroll" => "would perform mouse action (NOP)",
            _ => "would execute command"
        };
    }
}
