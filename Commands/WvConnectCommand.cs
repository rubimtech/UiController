using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvConnectCommand : ICommand
{
    public string Name => "wv-connect";
    public string Description => "Connect to WebView2 via CDP";
    public string Usage => "wv-connect [--port <port>] [--timeout <sec>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;
        int port = 9222;
        int timeout = 30;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[++i], out var p))
                port = p;
            else if (args[i] == "--timeout" && i + 1 < args.Length && int.TryParse(args[++i], out var t))
                timeout = t;
        }

        if (Program.WebView2 != null)
        {
            await Program.WebView2.DisconnectAsync();
            Program.WebView2 = null;
        }

        Program.WebView2 = new WebView2Client(port);
        try
        {
            await Program.WebView2.ConnectAsync(timeout);
        }
        catch (Exception ex)
        {
            Program.WebView2 = null;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-connect",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        var result = new CommandResult
        {
            Command = "wv-connect",
            Success = true,
            Data = new
            {
                port = Program.WebView2.Port,
                pages = Program.WebView2.PageCount,
                url = Program.WebView2.CurrentUrl,
                registryWasSet = WebView2Client.IsRegistrySetup()
            },
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return 0;
    }
}
