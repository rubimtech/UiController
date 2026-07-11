using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace UiController.Core.Commands;

public class ProcessListCommand : ICommand
{
    public string Name => "process-list";
    public string Description => "List all running processes. Usage: process-list [--filter name]";
    public string Usage => "process-list [--filter name]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        string? filter = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--filter" && i + 1 < args.Length)
                filter = args[++i];
        }

        var processes = Process.GetProcesses()
            .Select(p =>
            {
                try
                {
                    return new
                    {
                        pid = p.Id,
                        name = p.ProcessName,
                        title = p.MainWindowTitle ?? "",
                        memory = p.WorkingSet64
                    };
                }
                catch { return null; }
            })
            .Where(p => p != null);

        if (filter != null)
            processes = processes.Where(p =>
                p!.name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                p.title.Contains(filter, StringComparison.OrdinalIgnoreCase));

        var list = processes.Take(500).ToList();

        var result = new CommandResult
        {
            Command = "process-list",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { count = list.Count, filter }
                : new { count = list.Count, filter, processes = list }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}
