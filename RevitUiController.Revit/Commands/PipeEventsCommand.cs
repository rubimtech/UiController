using RevitUiController.Core;
using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Revit.Commands;

public class PipeEventsCommand : ICommand
{
    public string Name => "pipe-events";
    public string Description => "Show recent PipeEvents from Revit named pipe. Usage: pipe-events [--last N]";
    public string Usage => "pipe-events [--last N]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        int count = 20;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--last" && i + 1 < args.Length && int.TryParse(args[++i], out var n))
                count = n;
        }

        using var client = new PipeBridgeClient();
        if (!client.Connect())
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "pipe-events",
                Success = false,
                Error = "Failed to connect to ReVibe Named Pipe"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        Thread.Sleep(500);

        var events = client.EventQueue
            .TakeLast(count)
            .Select(e => new
            {
                type = e.Type,
                data = e.Data,
                timestamp = e.Timestamp.ToString("HH:mm:ss.fff")
            })
            .ToList();

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "pipe-events",
            Success = true,
            Data = new { total = events.Count, events }
        }, CoreSettings.GlobalOptions));

        return Task.FromResult(0);
    }
}
