using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class FindAllCommand : ICommand
{
    public string Name => "find-all";
    public string Description => "Find ALL controls matching a name, not just the first one. Usage: find-all <name> [--max N] [--type <ct>]";
    public string Usage => "find-all <name> [--max N] [--type <ct>]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("FindAllCommand", "Usage: find-all <name> [--max N] [--type <ct>]");
            return Task.FromResult(1);
        }

        var maxResults = 50;
        string? filterType = null;
        var name = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--max" && i + 1 < args.Length) int.TryParse(args[++i], out maxResults);
            else if (args[i] == "--type" && i + 1 < args.Length) filterType = args[++i];
            else name = (name == "" ? args[i] : name + " " + args[i]);
        }

        var results = AutomationHelper.FindControlsByName(revitWindow, name, maxResults);
        if (filterType != null)
            results = results.Where(e => { try { return e.ControlType.ToString().Contains(filterType, StringComparison.OrdinalIgnoreCase); } catch { return false; } }).ToList();

        if (results.Count == 0)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var elements = OutputFormatter.FromElementList(results);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "find-all", Success = true,
            Data = Program.Verbosity == "minimal"
                ? new { query = name, count = results.Count }
                : new { query = name, count = results.Count, results = elements }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}
