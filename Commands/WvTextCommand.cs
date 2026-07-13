using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvTextCommand : ICommand
{
    public string Name => "wv-text";
    public string Description => "Get text from an element in WebView2";
    public string Usage => "wv-text <selector>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length < 1)
        {
            Console.Write(OutputFormatter.FormatError("MissingSelector", "wv-text", ["Usage: wv-text <selector>"], Program.GlobalOptions));
            return 1;
        }

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-text", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return 1;
        }

        var selector = args[0];
        string? text;
        try
        {
            text = await Program.WebView2.GetTextAsync(selector);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-text",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-text",
            Success = true,
            Data = new { selector, text },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return 0;
    }
}
