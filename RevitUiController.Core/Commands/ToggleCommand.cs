using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class ToggleCommand : ICommand
{
    public string Name => "toggle";
    public string Description => "Toggle a checkbox/switch via TogglePattern. Usage: toggle <name> [on|off]";
    public string Usage => "toggle <name> [on|off]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("ToggleCommand", "Usage: toggle <name> [on|off]");
            return Task.FromResult(1);
        }

        bool? desiredState = null;
        string name;

        if (args.Length >= 2 && (args[^1].Equals("on", StringComparison.OrdinalIgnoreCase) || args[^1].Equals("off", StringComparison.OrdinalIgnoreCase) || args[^1].Equals("toggle", StringComparison.OrdinalIgnoreCase)))
        {
            var stateArg = args[^1];
            name = string.Join(" ", args[..^1]);
            if (stateArg.Equals("on", StringComparison.OrdinalIgnoreCase)) desiredState = true;
            else if (stateArg.Equals("off", StringComparison.OrdinalIgnoreCase)) desiredState = false;
        }
        else
        {
            name = string.Join(" ", args);
        }

        var element = AutomationHelper.FindFirstEnabledVisible(window, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        try
        {
            var toggle = element.Patterns.Toggle.Pattern;
            if (toggle == null)
            {
                Console.Write(OutputFormatter.FormatError("PatternNotSupported", name, ["TogglePattern not available"], CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }

            var before = toggle.ToggleState;
            if (!desiredState.HasValue)
            {
                toggle.Toggle();
            }
            else
            {
                var targetState = desiredState.Value ? ToggleState.On : ToggleState.Off;
                if (before != targetState)
                    toggle.Toggle();
            }

            var after = toggle.ToggleState;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "toggle", Success = true,
                Data = new { target = name, before = before.ToString(), after = after.ToString(), toggled = before != after }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("ToggleFailed", name, [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
