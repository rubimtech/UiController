using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvTypeCommand : ICommand
{
    public string Name => "wv-type";
    public string Description => "Type text into an element in WebView2";
    public string Usage => "wv-type <selector> <text>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length < 2)
        {
            Console.Write(OutputFormatter.FormatError("MissingArgs", "wv-type", ["Usage: wv-type <selector> <text>"], Program.GlobalOptions));
            return 1;
        }

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-type", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return 1;
        }

        var selector = args[0];
        var text = string.Join(" ", args.Skip(1));
        try
        {
            await Program.WebView2.TypeAsync(selector, text);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-type",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-type",
            Success = true,
            Data = new { selector, text },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return 0;
    }
}
