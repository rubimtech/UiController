using System.IO;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class RecordStartCommand : ICommand
{
    public string Name => "record-start";
    public string Description => "Start recording actions to an .rvs script: record-start <output-path>";
    public string Usage => "record-start <output-path>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("RecorderCommands", "Usage: record-start <output-path>");
            return Task.FromResult(1);
        }

        var path = string.Join(" ", args);
        if (!path.EndsWith(".rvs", StringComparison.OrdinalIgnoreCase))
            path += ".rvs";

        RecorderService.StartRecording(path);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "record-start",
            Success = true,
            Data = new { outputPath = path }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class RecordStopCommand : ICommand
{
    public string Name => "record-stop";
    public string Description => "Stop recording and save .rvs script";
    public string Usage => "record-stop";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (!RecorderService.IsRecording)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-stop",
                Success = false,
                Error = "No recording in progress"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var path = RecorderService.StopRecording();
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "record-stop",
            Success = path != null,
            Error = path == null ? "Failed to save recording" : null,
            Data = new { savedTo = path, actions = RecorderService.RecordedCount }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(path != null ? 0 : 1);
    }
}

public class RecordStatusCommand : ICommand
{
    public string Name => "record-status";
    public string Description => "Show recording status";
    public string Usage => "record-status";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "record-status",
            Success = true,
            Data = new { isRecording = RecorderService.IsRecording, actionsRecorded = RecorderService.RecordedCount }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class RecordSaveCommand : ICommand
{
    public string Name => "record-save";
    public string Description => "Save current recording, optionally showing diff against existing file";
    public string Usage => "record-save [--path <file.rvs>] [--diff]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (!RecorderService.IsRecording)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-save",
                Success = false,
                Error = "No recording in progress"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var customPath = ParseFlagValue(args, "--path");
        var showDiff = args.Any(a => a == "--diff");

        string? prevContent = null;
        var existingPath = customPath ?? RecorderService.GetRecordingPath();

        if (showDiff && existingPath != null && File.Exists(existingPath))
        {
            prevContent = File.ReadAllText(existingPath);
        }

        var savedPath = customPath != null
            ? RecorderService.SaveTo(customPath)
            : RecorderService.StopRecording();

        if (savedPath == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "record-save",
                Success = false,
                Error = "Failed to save recording"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        object? diffData = null;
        if (showDiff && prevContent != null)
        {
            var currentContent = File.ReadAllText(savedPath);
            diffData = new { previous = prevContent, current = currentContent, changed = prevContent != currentContent };
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "record-save",
            Success = true,
            Data = new
            {
                savedTo = savedPath,
                actions = RecorderService.RecordedCount,
                diff = diffData
            }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }

    private static string? ParseFlagValue(string[] args, string flag)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                return args[i + 1];
        }
        return null;
    }
}
