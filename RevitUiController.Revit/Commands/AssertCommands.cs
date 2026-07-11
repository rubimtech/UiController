using UiController.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Revit.Commands;

public class AssertDialogCommand : ICommand
{
    public string Name => "assert-dialog";
    public string Description => "Assert dialog state: assert-dialog <title> [exists|not-exists|text <expected>|button <name>|enabled <name>]";
    public string Usage => "assert-dialog <title> [exists|not-exists|text|button|enabled]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("AssertCommands", "Usage: assert-dialog <title> [check]");
            return Task.FromResult(1);
        }

        var title = args[0];
        var dialogs = FindActiveDialogs(window);
        var dialog = dialogs.FirstOrDefault(d =>
            (d.Name ?? "").Contains(title, StringComparison.OrdinalIgnoreCase));

        var check = args.Length > 1 ? args[1] : "exists";

        if (check == "exists")
        {
            var ok = dialog != null;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-dialog",
                Success = ok,
                Error = ok ? null : $"Dialog '{title}' is not open",
                Data = new { assert = "exists", title, actual = dialog?.Name }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }

        if (check == "not-exists")
        {
            var ok = dialog == null;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-dialog",
                Success = ok,
                Error = ok ? null : $"Dialog '{title}' is open but should not be",
                Data = new { assert = "not-exists", title }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }

        if (dialog == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-dialog",
                Success = false,
                Error = $"Dialog '{title}' is not open"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        if (check == "text" && args.Length >= 3)
        {
            var expected = string.Join(" ", args.Skip(2));
            var text = GetDialogText(dialog);
            var ok = text.Contains(expected, StringComparison.OrdinalIgnoreCase);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-dialog",
                Success = ok,
                Error = ok ? null : $"Dialog '{title}' does not contain text '{expected}'",
                Data = new { assert = "text", expected, actual = Truncate(text, 200) }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }

        if (check == "button" && args.Length >= 3)
        {
            var buttonName = string.Join(" ", args.Skip(2));
            var button = FindDialogButton(dialog, buttonName);
            var ok = button != null;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-dialog",
                Success = ok,
                Error = ok ? null : $"Button '{buttonName}' not found in dialog '{title}'",
                Data = new { assert = "button", name = buttonName, found = ok }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }

        if (check == "enabled" && args.Length >= 3)
        {
            var buttonName = string.Join(" ", args.Skip(2));
            var button = FindDialogButton(dialog, buttonName);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "assert-dialog",
                    Success = false,
                    Error = $"Button '{buttonName}' not found in dialog '{title}'"
                }, CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }
            var ok = button.IsEnabled;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-dialog",
                Success = ok,
                Error = ok ? null : $"Button '{buttonName}' is disabled",
                Data = new { assert = "enabled", name = buttonName, enabled = ok }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }

        if (check == "field" && args.Length >= 4)
        {
            var fieldLabel = args[2];
            var expectedValue = string.Join(" ", args.Skip(3));
            var field = FindFieldInDialog(dialog, fieldLabel);
            string? actualValue = null;
            if (field != null)
            {
                try
                {
                    if (field.Patterns.Value.Pattern != null)
                        actualValue = field.Patterns.Value.Pattern.Value;
                }
                catch { }
            }
            var ok = actualValue != null && actualValue.Contains(expectedValue, StringComparison.OrdinalIgnoreCase);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-dialog",
                Success = ok,
                Error = ok ? null : $"Field '{fieldLabel}' value '{actualValue}' does not contain '{expectedValue}'",
                Data = new { assert = "field", label = fieldLabel, expected = expectedValue, actual = actualValue }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }

        LoggingService.Error("AssertCommands", $"Unknown assert check: {check}");
        return Task.FromResult(1);
    }

    private static AutomationElement? FindFieldInDialog(AutomationElement dialog, string label)
    {
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Edit || c.ControlType == ControlType.ComboBox)
                    {
                        var name = c.Name ?? "";
                        if (name.Contains(label, StringComparison.OrdinalIgnoreCase))
                            return c;
                    }
                }
                catch { }
            }
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Text && c.Name != null &&
                        c.Name.Contains(label, StringComparison.OrdinalIgnoreCase))
                    {
                        var siblings = SafeGetChildren(dialog, 3000);
                        for (int i = 0; i < siblings.Length - 1; i++)
                        {
                            if ((siblings[i].Name ?? "") == c.Name && siblings[i].ControlType == c.ControlType)
                            {
                                var next = siblings[i + 1];
                                if (next.ControlType == ControlType.Edit || next.ControlType == ControlType.ComboBox)
                                    return next;
                            }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    private static string GetDialogText(AutomationElement dialog)
    {
        var texts = new List<string>();
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(dialog);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    var name = el.Name ?? "";
                    if (!string.IsNullOrEmpty(name) && (el.ControlType == ControlType.Text || el.ControlType == ControlType.Button))
                        texts.Add(name);
                    foreach (var c in SafeGetChildren(el, 2000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }
        return string.Join(" | ", texts.Distinct());
    }

    private static AutomationElement? FindDialogButton(AutomationElement dialog, string name)
    {
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(dialog);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    if (el.ControlType == ControlType.Button && (el.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
                        return el;
                    foreach (var c in SafeGetChildren(el, 2000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}

public class AssertRibbonCommand : ICommand
{
    public string Name => "assert-ribbon";
    public string Description => "Assert ribbon state: assert-ribbon <tab-name> [button <name>]";
    public string Usage => "assert-ribbon <tab-name> [button <name>]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("AssertCommands", "Usage: assert-ribbon <tab-name> [button <name>]");
            return Task.FromResult(1);
        }

        var tabName = args[0];
        var rootChildren = SafeGetChildren(window, 25000);

        var ribbonListFind = new Func<AutomationElement?>(() =>
        {
            foreach (var c in rootChildren)
            {
                try
                {
                    if (c.ControlType == ControlType.List)
                    {
                        foreach (var child in SafeGetChildren(c, 3000))
                        {
                            try
                            {
                                if (child.ControlType == ControlType.DataItem &&
                                    (child.Name == "UIFramework.RvtRibbonTab" || child.Name == "Autodesk.Windows.RibbonTab"))
                                    return c;
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            return null;
        });

        var ribbonList = ribbonListFind();
        if (ribbonList == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-ribbon",
                Success = false,
                Error = "Ribbon not found"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var tabExists = false;
        foreach (var item in SafeGetChildren(ribbonList, 3000))
        {
            try
            {
                if (item.ControlType == ControlType.DataItem)
                {
                    var autoId = item.AutomationId ?? "";
                    if (autoId.Contains(tabName, StringComparison.OrdinalIgnoreCase) ||
                        GetItemName(item).Contains(tabName, StringComparison.OrdinalIgnoreCase))
                    {
                        tabExists = true;
                        break;
                    }
                }
            }
            catch { }
        }

        if (args.Length == 1)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-ribbon",
                Success = tabExists,
                Error = tabExists ? null : $"Tab '{tabName}' not found in ribbon",
                Data = new { assert = "tab", name = tabName, exists = tabExists }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(tabExists ? 0 : 1);
        }

        if (args[1] == "button" && args.Length >= 3)
        {
            if (!tabExists)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "assert-ribbon",
                    Success = false,
                    Error = $"Tab '{tabName}' not found"
                }, CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }

            var buttonName = string.Join(" ", args.Skip(2));
            var button = FindFirstEnabledVisible(window, buttonName);
            var ok = button != null;
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-ribbon",
                Success = ok,
                Error = ok ? null : $"Button '{buttonName}' not found on tab '{tabName}'",
                Data = new { assert = "button", tab = tabName, name = buttonName, exists = ok }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(ok ? 0 : 1);
        }

        LoggingService.Error("AssertCommands", $"Unknown assert-ribbon check: {args[1]}");
        return Task.FromResult(1);
    }

    private static string GetItemName(AutomationElement dataItem)
    {
        try
        {
            foreach (var c in SafeGetChildren(dataItem, 2000))
            {
                try
                {
                    var n = c.Name ?? "";
                    if (!string.IsNullOrEmpty(n) && !n.StartsWith("UIFramework") && !n.StartsWith("Autodesk"))
                        return n;
                }
                catch { }
            }
        }
        catch { }
        return dataItem.Name ?? "";
    }
}

public class AssertViewCommand : ICommand
{
    public string Name => "assert-view";
    public string Description => "Assert view tab state: assert-view <name> [active]";
    public string Usage => "assert-view <name> [active]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("AssertCommands", "Usage: assert-view <name> [active]");
            return Task.FromResult(1);
        }

        var viewName = string.Join(" ", args).ToLowerInvariant();

        var rootChildren = SafeGetChildren(window, 10000);
        AutomationElement? tabControl = null;
        foreach (var c in rootChildren)
        {
            try
            {
                if (c.ControlType == ControlType.Tab)
                { tabControl = c; break; }
                if (c.ControlType != ControlType.Tab)
                {
                    foreach (var deep in SafeGetChildren(c, 3000))
                    {
                        try { if (deep.ControlType == ControlType.Tab) { tabControl = deep; break; } } catch { }
                    }
                    if (tabControl != null) break;
                }
            }
            catch { }
        }

        if (tabControl == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "assert-view",
                Success = false,
                Error = "View tab control not found"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var found = false;
        var allTabs = new List<string>();
        foreach (var tab in SafeGetChildren(tabControl, 3000))
        {
            try
            {
                if (tab.ControlType == ControlType.TabItem)
                {
                    var name = tab.Name ?? "";
                    allTabs.Add(name);
                    if (name.ToLowerInvariant().Contains(viewName) && tab.IsEnabled && tab.IsOffscreen == false)
                        found = true;
                }
            }
            catch { }
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "assert-view",
            Success = found,
            Error = found ? null : $"View tab matching '{viewName}' not found or not active",
            Data = new { assert = "view", name = viewName, exists = found, allTabs }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(found ? 0 : 1);
    }
}
