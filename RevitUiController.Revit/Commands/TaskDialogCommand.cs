using RevitUiController.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Core.Models;
using static RevitUiController.Core.AutomationHelper;

namespace RevitUiController.Revit.Commands;

public class TaskDialogCommand : ICommand
{
    public string Name => "taskdialog";
    public string Description => "Read or interact with a TaskDialog: taskdialog <title> [read|click <button>|expand]";
    public string Usage => "taskdialog <title> [read|click <button>|expand]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("TaskDialogCommand", "Usage: taskdialog <title> [read|click <button>|expand]");
            return 1;
        }

        var dialogTitle = args[0];
        var dialogs = FindActiveDialogs(window);
        var dialog = dialogs.FirstOrDefault(d =>
            (d.Name ?? "").Contains(dialogTitle, StringComparison.OrdinalIgnoreCase));

        if (dialog == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "taskdialog",
                Success = false,
                Error = $"Dialog '{dialogTitle}' not found"
            }, CoreSettings.GlobalOptions));
            return 1;
        }

        var action = args.Length > 1 ? args[1] : "read";

        if (action == "read")
        {
            var info = ReadTaskDialog(dialog);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "taskdialog",
                Success = true,
                Data = info
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        if (action == "expand")
        {
            ClickExpandButton(dialog);
            await Task.Delay(500, ct);
            var info = ReadTaskDialog(dialog);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "taskdialog",
                Success = true,
                Data = info
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        if (action == "click" && args.Length >= 3)
        {
            var buttonName = string.Join(" ", args.Skip(2));
            var buttons = FindTaskDialogButtons(dialog);
            var button = buttons.FirstOrDefault(b =>
                (b.Name ?? "").Contains(buttonName, StringComparison.OrdinalIgnoreCase));

            if (button == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "taskdialog",
                    Success = false,
                    Error = $"Button '{buttonName}' not found in task dialog"
                }, CoreSettings.GlobalOptions));
                return 1;
            }

            button.Click();
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "taskdialog",
                Success = true,
                Data = new { action = "click", button = buttonName }
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        LoggingService.Error("TaskDialogCommand", $"Unknown action: {action}");
        return 1;
    }

    private static object ReadTaskDialog(AutomationElement dialog)
    {
        var mainInstruction = "";
        var content = "";
        var footer = "";
        var buttons = new List<string>();
        var detailsExpanded = false;

        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    var ct = c.ControlType;
                    var name = c.Name ?? "";

                    if (ct == ControlType.Text && !string.IsNullOrEmpty(name))
                    {
                        if (name.Length > 50 && string.IsNullOrEmpty(content))
                            content = name;
                        else if (string.IsNullOrEmpty(mainInstruction))
                            mainInstruction = name;
                    }

                    if (ct == ControlType.Button && !string.IsNullOrEmpty(name))
                    {
                        if (name.Contains("Подробности", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Details", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains(">>", StringComparison.OrdinalIgnoreCase))
                        {
                            detailsExpanded = true;
                        }
                        else
                        {
                            buttons.Add(name);
                        }
                    }

                    if (ct == ControlType.Edit || ct == ControlType.Document)
                    {
                        if (!string.IsNullOrEmpty(name))
                        {
                            if (name.Length > 100)
                                detailsExpanded = true;
                            content = name;
                        }
                        try
                        {
                            if (c.Patterns.Text.Pattern != null)
                            {
                                var text = c.Patterns.Text.Pattern.DocumentRange.GetText(5000).Trim();
                                if (!string.IsNullOrEmpty(text)) { content = text; detailsExpanded = true; }
                            }
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }

        if (buttons.Count == 0)
            buttons.AddRange(FindCommonButtons(dialog));

        return new
        {
            title = dialog.Name,
            mainInstruction,
            content = Truncate(content, 500),
            footer,
            buttons,
            detailsExpanded
        };
    }

    private static List<AutomationElement> FindTaskDialogButtons(AutomationElement dialog)
    {
        var result = new List<AutomationElement>();
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Button && !string.IsNullOrEmpty(c.Name))
                        result.Add(c);
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static List<string> FindCommonButtons(AutomationElement dialog)
    {
        var names = new List<string>();
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(dialog);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    if (el.ControlType == ControlType.Button)
                    {
                        var n = el.Name ?? "";
                        if (!string.IsNullOrEmpty(n) && !n.Contains("Details") && !n.Contains("Подробности"))
                            names.Add(n);
                    }
                    foreach (var c in SafeGetChildren(el, 2000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }
        return names.Distinct().ToList();
    }

    private static void ClickExpandButton(AutomationElement dialog)
    {
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Button)
                    {
                        var n = c.Name ?? "";
                        if (n.Contains("Подробности", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains("Details", StringComparison.OrdinalIgnoreCase) ||
                            n.Contains(">>", StringComparison.OrdinalIgnoreCase))
                        {
                            c.Click();
                            return;
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
