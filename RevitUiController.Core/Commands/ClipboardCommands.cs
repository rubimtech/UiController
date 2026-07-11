using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class ClipboardGetCommand : ICommand
{
    public string Name => "clipboard-get";
    public string Description => "Read text from clipboard. Usage: clipboard-get";
    public string Usage => "clipboard-get";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        try
        {
            var text = System.Windows.Forms.Clipboard.GetText();
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "clipboard-get", Success = true,
                Data = new
                {
                    length = text?.Length ?? 0,
                    text = text ?? "",
                    truncated = text?.Length > 1000
                }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("ClipboardError", "read", [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}

public class ClipboardSetCommand : ICommand
{
    public string Name => "clipboard-set";
    public string Description => "Write text to clipboard. Usage: clipboard-set <text>";
    public string Usage => "clipboard-set <text>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var text = string.Join(" ", args);
        if (string.IsNullOrEmpty(text))
        {
            LoggingService.Error("ClipboardCommands", "Usage: clipboard-set <text>");
            return Task.FromResult(1);
        }

        try
        {
            System.Windows.Forms.Clipboard.SetText(text);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "clipboard-set", Success = true,
                Data = new { length = text.Length, preview = text.Length > 50 ? text[..50] + "..." : text }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("ClipboardError", "write", [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
