using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class SetValueCommand : ICommand
{
    public string Name => "set-value";
    public string Description => "Set a control's value via ValuePattern (more reliable than 'type' which uses SendKeys). Usage: set-value <name> <text>";
    public string Usage => "set-value <name> <text>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: set-value <name> <text>");
            return Task.FromResult(1);
        }

        var text = args[^1];
        var name = string.Join(" ", args[..^1]);

        var element = AutomationHelper.FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.IsPretty));
            return Task.FromResult(1);
        }

        try
        {
            var value = element.Patterns.Value.Pattern;
            if (value == null)
            {
                Console.Write(OutputFormatter.FormatError("PatternNotSupported", name, ["ValuePattern not available, try type"], Program.IsPretty));
                return Task.FromResult(1);
            }
            value.SetValue(text);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "set-value", Success = true,
                Data = new { target = name, value = text, method = "ValuePattern.SetValue" }
            }, Program.IsPretty));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("SetValueFailed", name, [ex.Message], Program.IsPretty));
            return Task.FromResult(1);
        }
    }
}
