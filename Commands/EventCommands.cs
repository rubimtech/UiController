using UiController.Core.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class ListenStartCommand : ICommand
{
    public string Name => "listen-start";
    public string Description => "Start event-driven automation listener for reactive UI monitoring";
    public string Usage => "listen-start";

    public Task<int> ExecuteAsync(FlaUI.Core.AutomationElements.AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var evt = Program.CurrentSession?.Automation;
        if (evt == null)
        {
            LoggingService.Error("EventCommands", "No automation session available. Connect to a window first.");
            return Task.FromResult(1);
        }

        if (Program.EventService != null && Program.EventService.IsListening)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "listen-start",
                Success = true,
                Data = new { status = "already_listening" }
            }, Program.GlobalOptions));
            return Task.FromResult(0);
        }

        var desktop = evt.GetDesktop();
        Program.EventService?.Dispose();
        Program.EventService = new AutomationEventService(evt, revitWindow);
        Program.EventService.StartListening();

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "listen-start",
            Success = true,
            Data = new { status = "started", events = new[] { "FocusChanged", "StructureChanged", "WindowOpened", "WindowClosed" } }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class ListenStopCommand : ICommand
{
    public string Name => "listen-stop";
    public string Description => "Stop event-driven automation listener";
    public string Usage => "listen-stop";

    public Task<int> ExecuteAsync(FlaUI.Core.AutomationElements.AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (Program.EventService == null || !Program.EventService.IsListening)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "listen-stop",
                Success = true,
                Data = new { status = "not_listening" }
            }, Program.GlobalOptions));
            return Task.FromResult(0);
        }

        Program.EventService.StopListening();

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "listen-stop",
            Success = true,
            Data = new { status = "stopped" }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class EventLogCommand : ICommand
{
    public string Name => "event-log";
    public string Description => "Show recent automation events. Usage: event-log [--last N]";
    public string Usage => "event-log [--last N]";

    public Task<int> ExecuteAsync(FlaUI.Core.AutomationElements.AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        int count = 20;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--last" && i + 1 < args.Length && int.TryParse(args[++i], out var n))
                count = n;
        }

        if (Program.EventService == null || !Program.EventService.IsListening)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "event-log",
                Success = true,
                Data = new { status = "not_listening", events = Array.Empty<object>() }
            }, Program.GlobalOptions));
            return Task.FromResult(0);
        }

        var events = Program.EventService.GetRecentEvents(count)
            .Select(e => new
            {
                type = e.Type,
                name = e.Name,
                automationId = e.AutomationId,
                controlType = e.ControlType,
                timestamp = e.Timestamp.ToString("HH:mm:ss.fff")
            })
            .ToList();

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "event-log",
            Success = true,
            Data = new { total = events.Count, events }
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}
