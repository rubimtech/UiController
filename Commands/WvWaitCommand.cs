using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvWaitCommand : ICommand
{
    public string Name => "wv-wait";
    public string Description => "Wait for an element to appear in WebView2";
    public string Usage => "wv-wait <selector> [--timeout <sec>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length < 1)
        {
            Console.Write(OutputFormatter.FormatError("MissingSelector", "wv-wait", ["Usage: wv-wait <selector> [--timeout <sec>]"], Program.GlobalOptions));
            return 1;
        }

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-wait", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return 1;
        }

        var selector = args[0];
        int timeout = 5;

        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--timeout" && i + 1 < args.Length && int.TryParse(args[++i], out var t))
                timeout = t;
        }

        try
        {
            await Program.WebView2.WaitForSelectorAsync(selector, timeout);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-wait",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-wait",
            Success = true,
            Data = new { selector, timeout },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return 0;
    }
}
