using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class KeyComboCommand : ICommand
{
    public string Name => "key-combo";
    public string Description => "Send keyboard shortcut via SendKeys. Usage: key-combo <keys> (e.g. \"^c\" for Ctrl+C, \"%{F4}\" for Alt+F4, \"^+s\" for Ctrl+Shift+S)";
    public string Usage => "key-combo <keys>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var keys = string.Join(" ", args);
        if (string.IsNullOrEmpty(keys))
        {
            LoggingService.Error("KeyComboCommand", "Usage: key-combo <keys>\nExamples:\n  ^c  = Ctrl+C\n  ^v  = Ctrl+V\n  %{F4} = Alt+F4\n  {TAB} = Tab\n  {ENTER} = Enter\n  ^+s = Ctrl+Shift+S");
            return Task.FromResult(1);
        }

        try
        {
            System.Windows.Forms.SendKeys.SendWait(keys);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "key-combo", Success = true,
                Data = new { keys, method = "SendKeys.SendWait" }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("KeyComboFailed", keys, [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
