using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvListCommand : ICommand
{
    public string Name => "wv-list";
    public string Description => "List interactive elements in WebView2";
    public string Usage => "wv-list [--filter <text>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-list", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return 1;
        }

        string? filter = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--filter" && i + 1 < args.Length)
                filter = args[++i];
        }

        List<ElementInfo> elements;
        try
        {
            elements = await Program.WebView2.GetSelectorsAsync(filter);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-list",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-list",
            Success = true,
            Data = new
            {
                count = elements.Count,
                elements
            },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return 0;
    }
}
