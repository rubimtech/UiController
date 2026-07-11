using System.IO;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace UiController.Core.Commands;

public class WindowScreenshotCommand : ICommand
{
    public string Name => "window-screenshot";
    public string Description => "Take screenshot of the connected window. Usage: window-screenshot [--output path.png]";
    public string Usage => "window-screenshot [--output path.png]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        string? outputPath = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output" && i + 1 < args.Length)
                outputPath = args[++i];
        }

        var b64 = ScreenshotHelper.CaptureWindow(window);
        if (b64 == null)
        {
            Console.Write(OutputFormatter.FormatError("ScreenshotFailed", "", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        if (outputPath != null)
        {
            try
            {
                var bytes = Convert.FromBase64String(b64);
                File.WriteAllBytes(outputPath, bytes);
            }
            catch (Exception ex)
            {
                Console.Write(OutputFormatter.FormatError("WriteFailed", outputPath, [ex.Message], CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }
        }

        var result = new CommandResult
        {
            Command = "window-screenshot",
            Success = true,
            Screenshot = outputPath == null ? b64 : null,
            Data = outputPath != null
                ? new { output = outputPath, size = Convert.FromBase64String(b64).Length }
                : new { format = "base64-png" }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}
