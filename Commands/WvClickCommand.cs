using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvClickCommand : ICommand
{
    public string Name => "wv-click";
    public string Description => "Click an element in WebView2";
    public string Usage => "wv-click <selector>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length < 1)
        {
            Console.Write(OutputFormatter.FormatError("MissingSelector", "wv-click", ["Usage: wv-click <selector>"], Program.GlobalOptions));
            return 1;
        }

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-click", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return 1;
        }

        var selector = args[0];
        try
        {
            await Program.WebView2.ClickAsync(selector);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-click",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-click",
            Success = true,
            Data = new { selector },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return 0;
    }
}
