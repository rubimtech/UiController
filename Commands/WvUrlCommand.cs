using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvUrlCommand : ICommand
{
    public string Name => "wv-url";
    public string Description => "Get current URL of WebView2";
    public string Usage => "wv-url";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-url", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return Task.FromResult(1);
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-url",
            Success = true,
            Data = new { url = Program.WebView2.CurrentUrl },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}
