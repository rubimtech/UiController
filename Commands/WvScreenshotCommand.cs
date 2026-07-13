using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace RevitUiController.Commands;

public class WvScreenshotCommand : ICommand
{
    public string Name => "wv-screenshot";
    public string Description => "Take a screenshot of WebView2";
    public string Usage => "wv-screenshot [--path <file.png>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (Program.WebView2 == null || !Program.WebView2.IsConnected)
        {
            Console.Write(OutputFormatter.FormatError("NotConnected", "wv-screenshot", ["Use 'wv-connect' first."], Program.GlobalOptions));
            return 1;
        }

        string? path = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--path" && i + 1 < args.Length)
                path = args[++i];
        }

        string? b64;
        try
        {
            b64 = await Program.WebView2.ScreenshotAsync();
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wv-screenshot",
                Success = false,
                Error = ex.Message,
                DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
            }, Program.GlobalOptions));
            return 1;
        }

        object data;
        if (path != null && b64 != null)
        {
            var bytes = Convert.FromBase64String(b64);
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllBytesAsync(path, bytes, ct);
            data = new { path };
        }
        else
        {
            data = new { screenshotBase64 = b64 };
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wv-screenshot",
            Success = b64 != null,
            Error = b64 == null ? "Screenshot returned null" : null,
            Data = data,
            DurationMs = (DateTime.UtcNow - start).TotalMilliseconds
        }, Program.GlobalOptions));
        return b64 != null ? 0 : 1;
    }
}
