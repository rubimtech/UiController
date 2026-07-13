using System.Text.Json;
using UiController.Core;
using UiController.Core.Protocol;
using RevitUiController.Revit;

namespace UiController.Daemon;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            _cts.Cancel();
        };

        var pipeName = "RevitUiController";
        var profile = "Revit";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pipe" when i + 1 < args.Length:
                    pipeName = args[++i];
                    break;
                case "--profile" when i + 1 < args.Length:
                    profile = args[++i];
                    break;
                case "--auto-screenshot":
                    DaemonSettings.AutoScreenshot = true;
                    break;
                case "--daemon":
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    return 0;
            }
        }

        IApplicationProfile appProfile;
        if (profile.Equals("Revit", StringComparison.OrdinalIgnoreCase))
            appProfile = new RevitProfile();
        else
            appProfile = new GenericProfile(profile);

        CoreSettings.CurrentProfile = appProfile;

        var server = new DaemonServer(pipeName, appProfile);
        RegisterCommands(server);

        LoggingService.Info("Daemon", $"Starting RevitUiController Daemon (pipe={pipeName}, profile={profile})");
        LoggingService.Info("Daemon", $"PID: {Environment.ProcessId}");

        if (args.Contains("--daemon"))
        {
            await server.StartAsync(_cts.Token);
            LoggingService.Info("Daemon", "Daemon started. Press Ctrl+C to stop.");
            try { await Task.Delay(Timeout.Infinite, _cts.Token); }
            catch (OperationCanceledException) { }
            LoggingService.Info("Daemon", "Shutting down...");
            server.Dispose();
            return 0;
        }

        if (args.Length == 0 || args[0] == "interactive")
        {
            await server.StartAsync(_cts.Token);
            Console.WriteLine($"RevitUiController Daemon (pipe: {pipeName})");
            Console.WriteLine("Type commands (JSON) or 'quit' to exit.");
            Console.WriteLine("Shortcuts: connect, ping, shutdown, batch {...}");

            while (!_cts.IsCancellationRequested && !server.IsShutdownRequested)
            {
                var line = Console.ReadLine();
                if (line == null || line == "quit" || line == "exit") break;
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Expand shortcuts to JSON
                var json = line.TrimStart() switch
                {
                    var s when s.StartsWith("{") => s,
                    var s when s.StartsWith("connect", StringComparison.OrdinalIgnoreCase) =>
                        JsonSerializer.Serialize(new DaemonRequest { Command = "__connect", ProcessName = "Revit" }),
                    var s when s.Equals("ping", StringComparison.OrdinalIgnoreCase) =>
                        JsonSerializer.Serialize(new DaemonRequest { Command = "__ping" }),
                    var s when s.Equals("shutdown", StringComparison.OrdinalIgnoreCase) =>
                        JsonSerializer.Serialize(new DaemonRequest { Command = "__shutdown" }),
                    var s when s.StartsWith("batch", StringComparison.OrdinalIgnoreCase) =>
                        "{" + string.Join(" ", s.Split(' ').Skip(1)) + "}",
                    _ => JsonSerializer.Serialize(new DaemonRequest { Command = line.Trim() })
                };

                var response = await server.ProcessRequest(json, _cts.Token);
                try
                {
                    var formatted = JsonSerializer.Serialize(
                        JsonSerializer.Deserialize<object>(response),
                        new JsonSerializerOptions { WriteIndented = true });
                    Console.WriteLine(formatted);
                }
                catch
                {
                    Console.WriteLine(response);
                }
            }

            server.Dispose();
            return 0;
        }

        using var client = new DaemonClient(pipeName);
        if (!client.Connect(5000))
        {
            Console.Error.WriteLine($"Cannot connect to daemon on pipe '{pipeName}'.");
            Console.Error.WriteLine("Start the daemon first:");
            Console.Error.WriteLine($"  {Path.GetFileName(Environment.ProcessPath!)} --daemon");
            return 1;
        }

        var request = BuildRequest(args);
        var responseText = client.SendRequest(request);
        if (responseText != null) Console.Write(responseText);
        return 0;
    }

    private static DaemonRequest BuildRequest(string[] args)
    {
        if (args.Length == 0) return new DaemonRequest();

        var cmdName = args[0].ToLowerInvariant();

        if (cmdName == "connect" || cmdName == "__connect")
        {
            var r = new DaemonRequest { Command = "__connect" };
            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--pid" when i + 1 < args.Length: r.Pid = int.Parse(args[++i]); break;
                    case "--process-name" when i + 1 < args.Length: r.ProcessName = args[++i]; break;
                    case "--window-title" when i + 1 < args.Length: r.WindowTitle = args[++i]; break;
                    case "--active": r.UseActive = true; break;
                    case "--timeout" when i + 1 < args.Length: r.Timeout = int.Parse(args[++i]); break;
                }
            }
            return r;
        }

        if (cmdName is "batch" or "__batch")
        {
            var r = new DaemonRequest { Command = "__batch" };
            if (args.Length > 1)
            {
                try
                {
                    var batchRequest = JsonSerializer.Deserialize<DaemonRequest>(string.Join(" ", args.Skip(1)));
                    if (batchRequest?.Commands != null) r.Commands = batchRequest.Commands;
                }
                catch { }
            }
            return r;
        }

        if (cmdName is "undo" or "__undo")
        {
            var r = new DaemonRequest { Command = "__undo", Action = args.Length > 1 ? args[1] : "status" };
            if (r.Action is "undo" or "rollback")
            {
                for (int i = 2; i < args.Length; i++)
                    if (args[i] == "--count" && i + 1 < args.Length) r.Count = int.Parse(args[++i]);
            }
            return r;
        }

        return new DaemonRequest { Command = cmdName, Args = args.Skip(1).ToList() };
    }

    private static void RegisterCommands(DaemonServer server)
    {
        foreach (var type in typeof(ICommand).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(ICommand).IsAssignableFrom(t)))
        {
            if (Activator.CreateInstance(type) is ICommand cmd)
                server.RegisterCommand(cmd);
        }
    }

    private static readonly CancellationTokenSource _cts = new();

    private static void PrintHelp()
    {
        Console.WriteLine("""
RevitUiController Daemon — persistent-mode UI automation server

Modes:
  --daemon              Start as background server (named pipe listener)
  <command> [args]      Send command to existing daemon instance
  interactive           Start daemon with interactive REPL

Options:
  --pipe <name>         Named pipe name (default: RevitUiController)
  --profile <name>      App profile (default: Revit)
  --auto-screenshot     Auto-capture screenshot after every command

Daemon protocol (line-delimited JSON via named pipe):
  {"command":"__connect","processName":"Revit"}     Connect to process
  {"command":"__ping"}                                Health check
  {"command":"__shutdown"}                            Stop daemon
  {"command":"__batch","commands":[...]}              Execute batch
  {"command":"__watch","subCommand":"click",...}      Watch with polling
  {"command":"__undo","action":"status|undo"}         Undo management
  {"command":"__events","timeout":30}                 Read event stream
  {"command":"click","args":["OK"]}                   Execute any command
""");
    }
}
