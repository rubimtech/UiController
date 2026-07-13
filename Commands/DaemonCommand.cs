using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class DaemonCommand : ICommand
{
    public string Name => "daemon";
    public string Description => "Start or interact with the persistent RevitUiController daemon";
    public string Usage => "daemon [--start|--stop|--status|--pipe <name>] [--profile <name>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var action = "status";
        var pipeName = "RevitUiController";
        var profile = "Revit";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--start": action = "start"; break;
                case "--stop": action = "stop"; break;
                case "--status": action = "status"; break;
                case "--pipe" when i + 1 < args.Length: pipeName = args[++i]; break;
                case "--profile" when i + 1 < args.Length: profile = args[++i]; break;
            }
        }

        switch (action)
        {
            case "start": return await StartDaemon(pipeName, profile, ct);
            case "stop": return await StopDaemon(pipeName, ct);
            default: return await CheckStatus(pipeName, ct);
        }
    }

    private async Task<int> StartDaemon(string pipeName, string profile, CancellationToken ct)
    {
        var daemonExe = FindDaemonExecutable();
        if (daemonExe == null)
        {
            Console.Write(OutputFormatter.FormatError(UiController.Core.Models.ErrorCode.InternalError,
                "daemon executable not found. Build RevitUiController.Daemon first."));
            return 1;
        }

        var existing = Process.GetProcessesByName("RevitUiController.Daemon");
        if (existing.Length > 0)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "daemon", Success = true,
                Data = new { message = "Daemon already running", pid = existing[0].Id }
            }, Program.GlobalOptions));
            return 0;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = daemonExe,
                Arguments = $"--daemon --pipe \"{pipeName}\" --profile {profile}",
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                Console.Write(OutputFormatter.FormatError(UiController.Core.Models.ErrorCode.InternalError, "Failed to start daemon process"));
                return 1;
            }

            await Task.Delay(1000, ct);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "daemon", Success = true,
                Data = new { message = "Daemon started", pid = process.Id, pipe = pipeName }
            }, Program.GlobalOptions));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError(UiController.Core.Models.ErrorCode.InternalError, $"Failed to start daemon: {ex.Message}"));
            return 1;
        }
    }

    private async Task<int> StopDaemon(string pipeName, CancellationToken ct)
    {
        using var client = new SimplePipeClient(pipeName);
        if (client.Connect(2000))
            client.SendRaw("{\"command\":\"__shutdown\"}");
        else
        {
            foreach (var p in Process.GetProcessesByName("RevitUiController.Daemon"))
            {
                try { p.Kill(); } catch { }
            }
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "daemon", Success = true,
            Data = new { message = "Daemon stopped" }
        }, Program.GlobalOptions));
        return 0;
    }

    private async Task<int> CheckStatus(string pipeName, CancellationToken ct)
    {
        var processes = Process.GetProcessesByName("RevitUiController.Daemon");
        var isRunning = processes.Length > 0;

        using var client = new SimplePipeClient(pipeName);
        var pipeConnected = client.Connect(1000);
        string? pingResult = null;

        if (pipeConnected)
        {
            pingResult = client.SendRaw("{\"command\":\"__ping\"}");
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "daemon", Success = true,
            Data = new
            {
                running = isRunning,
                pipeConnected,
                pipeName,
                pids = processes.Select(p => p.Id).ToList(),
                pingResponse = pingResult
            }
        }, Program.GlobalOptions));
        return 0;
    }

    private static string? FindDaemonExecutable()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "..", "..", "..", "..", "RevitUiController.Daemon", "bin", "Debug", "net10.0-windows", "RevitUiController.Daemon.exe"),
            Path.Combine(baseDir, "..", "..", "..", "..", "RevitUiController.Daemon", "bin", "Release", "net10.0-windows", "RevitUiController.Daemon.exe"),
            Path.Combine(baseDir, "RevitUiController.Daemon.exe"),
            Path.Combine(Path.GetDirectoryName(baseDir) ?? "", "RevitUiController.Daemon", "RevitUiController.Daemon.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}

internal class SimplePipeClient : IDisposable
{
    private readonly string _pipeName;
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public SimplePipeClient(string pipeName) { _pipeName = pipeName; }

    public bool Connect(int timeoutMs)
    {
        try
        {
            _pipe = new NamedPipeClientStream(".", _pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipe.Connect(timeoutMs);
            _reader = new StreamReader(_pipe);
            _writer = new StreamWriter(_pipe) { AutoFlush = true };
            return true;
        }
        catch { return false; }
    }

    public string? SendRaw(string json)
    {
        if (_pipe == null || !_pipe.IsConnected || _writer == null || _reader == null) return null;
        try { _writer.WriteLine(json); return _reader.ReadLine(); }
        catch { return null; }
    }

    public void Dispose() { _pipe?.Dispose(); }
}
