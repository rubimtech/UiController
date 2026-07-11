using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class RetryClickCommand : ICommand
{
    public string Name => "retry-click";
    public string Description => "Find and click with exponential backoff retry: retry-click <name> [--attempts N] [--delay Ms]";
    public string Usage => "retry-click <name> [--attempts N] [--delay Ms]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("retry-click", "Usage: retry-click <name> [--attempts N] [--delay Ms]");
            return 1;
        }

        var name = string.Join(" ", args.Where(a => !a.StartsWith("--")));
        var attempts = 3;
        var delay = 500;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--attempts" && i + 1 < args.Length) attempts = int.Parse(args[++i]);
            if (args[i] == "--delay" && i + 1 < args.Length) delay = int.Parse(args[++i]);
        }

        var before = OutputFormatter.CaptureState(window);
        var found = await RetryPolicy.RetryAsync(
            () => Task.FromResult(AutomationHelper.FindFirstEnabledVisible(window, name)),
            attempts, delay, RetryPolicy.BackoffMode.Fixed, ct: ct);

        if (found == null)
        {
            var after = OutputFormatter.CaptureState(window);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "retry-click",
                Success = false,
                Error = $"Element '{name}' not found after {attempts} retries",
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, CoreSettings.GlobalOptions));
            return 1;
        }

        var clicked = await RetryPolicy.RetryActionAsync(
            () => { found.Click(); return Task.CompletedTask; },
            attempts, delay, RetryPolicy.BackoffMode.Fixed, ct);
        var after2 = OutputFormatter.CaptureState(window);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "retry-click",
            Success = clicked,
            Error = clicked ? null : $"Failed to click '{name}' after {attempts} retries",
            Diff = OutputFormatter.ComputeDiff(before, after2)
        }, CoreSettings.GlobalOptions));
        return clicked ? 0 : 1;
    }
}

public class RetryDialogCommand : ICommand
{
    public string Name => "retry-dialog";
    public string Description => "Wait for dialog with retry: retry-dialog <title> [--attempts N] [--delay Ms]";
    public string Usage => "retry-dialog <title> [--attempts N] [--delay Ms]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("RetryCommands", "Usage: retry-dialog <title> [--attempts N] [--delay Ms]");
            return 1;
        }

        var title = args[0];
        var attempts = 5;
        var delay = 500;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--attempts" && i + 1 < args.Length) attempts = int.Parse(args[++i]);
            if (args[i] == "--delay" && i + 1 < args.Length) delay = int.Parse(args[++i]);
        }

        var dialog = await RetryPolicy.RetryAsync(
            () => Task.Run(() => Retry.WaitForDialog(window, title, attempts * delay, delay)),
            attempts, delay, RetryPolicy.BackoffMode.Fixed, ct: ct);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "retry-dialog",
            Success = dialog != null,
            Error = dialog == null ? $"Dialog '{title}' not found after {attempts} retries" : null,
            Data = new { title, found = dialog != null, name = dialog?.Name }
        }, CoreSettings.GlobalOptions));
        return dialog != null ? 0 : 1;
    }
}
