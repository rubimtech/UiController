using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvEvalCommand : ICommand
{
    public string Name => "wv-eval";
    public string Description => "Execute JavaScript in WebView2";
    public string Usage => "wv-eval <js>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length < 1)
        {
            Console.Write(OutputFormatter.FormatError("MissingJs", "wv-eval", ["Usage: wv-eval <js>"], Program.GlobalOptions));
            return 1;
        }

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-eval", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return 1;
        }

        var js = string.Join(" ", args);
        object? result;
        try
        {
            var raw = await Program.WebView2.EvaluateAsync<System.Text.Json.JsonElement?>(js);
            result = raw.HasValue ? raw.Value.ToString() : null;
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-eval",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-eval",
            Success = true,
            Data = new { result },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return 0;
    }
}
