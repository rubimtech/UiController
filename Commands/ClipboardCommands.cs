using System.Runtime.InteropServices;
using System.Text;
using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class ClipboardGetCommand : ICommand
{
    public string Name => "clipboard-get";
    public string Description => "Read text from clipboard. Usage: clipboard-get";
    public string Usage => "clipboard-get";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            string? text = null;
            var thread = new Thread(() =>
            {
                try
                {
                    text = System.Windows.Forms.Clipboard.GetText();
                }
                catch (Exception ex)
                {
                    text = null;
                    Console.Write(OutputFormatter.FormatError("ClipboardError", "read", [ex.Message], Program.GlobalOptions));
                    return;
                }
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "clipboard-get", Success = true,
                    Data = new
                    {
                        length = text?.Length ?? 0,
                        text = text ?? "",
                        truncated = text?.Length > 1000
                    }
                }, Program.GlobalOptions));
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(5000))
            {
                thread.Interrupt();
                Console.Write(OutputFormatter.FormatError("ClipboardError", "read", ["Clipboard access timed out"], Program.GlobalOptions));
                return 1;
            }
            if (text == null && thread.ThreadState != ThreadState.Stopped && thread.ThreadState != ThreadState.Aborted)
                return 1;
            return text != null ? 0 : 1;
        }, ct);
    }
}

public class ClipboardSetCommand : ICommand
{
    public string Name => "clipboard-set";
    public string Description => "Write text to clipboard. Usage: clipboard-set <text>";
    public string Usage => "clipboard-set <text>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var text = string.Join(" ", args);
        if (string.IsNullOrEmpty(text))
        {
            LoggingService.Error("ClipboardCommands", "Usage: clipboard-set <text>");
            return Task.FromResult(1);
        }

        return Task.Run(() =>
        {
            var success = false;
            var thread = new Thread(() =>
            {
                try
                {
                    System.Windows.Forms.Clipboard.SetText(text);
                    success = true;
                    Console.Write(OutputFormatter.FormatResult(new CommandResult
                    {
                        Command = "clipboard-set", Success = true,
                        Data = new { length = text.Length, preview = text.Length > 50 ? text[..50] + "..." : text }
                    }, Program.GlobalOptions));
                }
                catch (Exception ex)
                {
                    Console.Write(OutputFormatter.FormatError("ClipboardError", "write", [ex.Message], Program.GlobalOptions));
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            if (!thread.Join(5000))
            {
                thread.Interrupt();
                Console.Write(OutputFormatter.FormatError("ClipboardError", "write", ["Clipboard access timed out"], Program.GlobalOptions));
                return 1;
            }
            return success ? 0 : 1;
        }, ct);
    }
}
