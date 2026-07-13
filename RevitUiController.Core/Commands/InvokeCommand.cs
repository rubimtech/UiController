using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class InvokeCommand : ICommand
{
    public string Name => "invoke";
    public string Description => "Invoke a control via InvokePattern (more reliable than Click for some buttons). Usage: invoke <name>";
    public string Usage => "invoke <name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var name = string.Join(" ", args);
        if (string.IsNullOrEmpty(name))
        {
            LoggingService.Error("InvokeCommand", "Usage: invoke <name>");
            return Task.FromResult(1);
        }

        var element = AutomationHelper.FindFirstEnabledVisible(window, name);
        if (element == null)
        {
            var similar = AutomationHelper.FindSimilarElementNames(window, name);
            var suggestions = new List<string>
            {
                "Try 'ai-find \"" + name + "\"' for multi-strategy search",
                "Try 'click \"" + name + "\"' as fallback",
                "Try 'list-controls' to see available elements"
            };
            if (similar.Count > 0)
                suggestions.Add("Similar: " + string.Join(", ", similar.Take(3)));
            Console.Write(OutputFormatter.FormatError("NotFound", name, suggestions, options: CoreSettings.GlobalOptions, availableElements: similar));
            return Task.FromResult(1);
        }

        try
        {
            var invoke = element.Patterns.Invoke.Pattern;
            if (invoke == null)
            {
                Console.Write(OutputFormatter.FormatError("PatternNotSupported", name, ["InvokePattern not available, try click"], CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }
            invoke.Invoke();
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "invoke", Success = true,
                Data = new { target = name, method = "InvokePattern" }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("InvokeFailed", name, [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
