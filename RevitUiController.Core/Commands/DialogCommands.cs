using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class PropertySheetCommand : ICommand
{
    public string Name => "ps";
    public string Description => "Read or interact with a PropertySheet dialog: ps <dialog-title> [action]";
    public string Usage => "ps <dialog-title> [fields|tabs|type <label> <value>|check <label>|select <label> <option>|click <button>]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("DialogCommands", "Usage: ps <dialog-title> [fields|tabs|type|check|select|click]");
            return 1;
        }

        var dialogTitle = args[0];
        var dialog = FindActiveDialog(window, dialogTitle);
        if (dialog == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps",
                Success = false,
                Error = $"Dialog '{dialogTitle}' not found"
            }, CoreSettings.GlobalOptions));
            return 1;
        }

        if (args.Length == 1 || args[1] == "fields")
        {
            var fields = ReadFields(dialog);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps",
                Success = true,
                Data = new { dialog = dialog.Name, fields }
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        if (args[1] == "tabs")
        {
            var tabs = ReadTabs(dialog);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps",
                Success = true,
                Data = new { dialog = dialog.Name, tabs }
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        if (args[1] == "type" && args.Length >= 4)
        {
            var label = args[2];
            var value = string.Join(" ", args.Skip(3));
            var field = FindFieldByLabel(dialog, label);
            if (field == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps",
                    Success = false,
                    Error = $"Field with label '{label}' not found"
                }, CoreSettings.GlobalOptions));
                return 1;
            }
            field.Focus();
            field.Click();
            await Task.Delay(100, ct);
            SendTextSafe(field, value);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps",
                Success = true,
                Data = new { action = "type", label, value }
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        if (args[1] == "check" && args.Length >= 3)
        {
            var label = args[2];
            var newState = args.Length >= 4 ? args[3].ToLowerInvariant() == "true" || args[3] == "1" : (bool?)null;
            var checkbox = FindCheckboxByLabel(dialog, label);
            if (checkbox == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps",
                    Success = false,
                    Error = $"Checkbox with label '{label}' not found"
                }, CoreSettings.GlobalOptions));
                return 1;
            }
            var currentState = GetIsChecked(checkbox);
            if (newState == null || newState == currentState)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps",
                    Success = true,
                    Data = new { action = "read", label, @checked = currentState }
                }, CoreSettings.GlobalOptions));
                return 0;
            }
            checkbox.Click();
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps",
                Success = true,
                Data = new { action = "toggle", label, from = currentState, to = newState }
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        if (args[1] == "select" && args.Length >= 4)
        {
            var label = args[2];
            var option = string.Join(" ", args.Skip(3));
            var combo = FindComboByLabel(dialog, label);
            if (combo == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps",
                    Success = false,
                    Error = $"ComboBox with label '{label}' not found"
                }, CoreSettings.GlobalOptions));
                return 1;
            }
            combo.Click();
            await Task.Delay(300, ct);
            var dropdownItem = FindDropdownItem(combo, option);
            if (dropdownItem != null)
            {
                dropdownItem.Click();
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps",
                    Success = true,
                    Data = new { action = "select", label, option }
                }, CoreSettings.GlobalOptions));
                return 0;
            }
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps",
                Success = false,
                Error = $"Option '{option}' not found in combobox '{label}'"
            }, CoreSettings.GlobalOptions));
            return 1;
        }

        if (args[1] == "click" && args.Length >= 3)
        {
            var buttonName = string.Join(" ", args.Skip(2));
            var button = FindDialogButton(dialog, buttonName);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps",
                    Success = false,
                    Error = $"Button '{buttonName}' not found in dialog"
                }, CoreSettings.GlobalOptions));
                return 1;
            }
            button.Click();
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps",
                Success = true,
                Data = new { action = "click", button = buttonName }
            }, CoreSettings.GlobalOptions));
            return 0;
        }

        LoggingService.Error("DialogCommands", $"Unknown action: {args[1]}");
        return 1;
    }

    private static AutomationElement? FindActiveDialog(AutomationElement root, string name)
    {
        var dialogs = FindActiveDialogs(root);
        return dialogs.FirstOrDefault(d =>
            (d.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    private static List<object> ReadFields(AutomationElement dialog)
    {
        var fields = new List<object>();
        try
        {
            var stack = new Queue<(AutomationElement el, int depth)>();
            stack.Enqueue((dialog, 0));
            while (stack.Count > 0)
            {
                var (el, depth) = stack.Dequeue();
                if (depth > 4) continue;
                foreach (var c in SafeGetChildren(el, 3000))
                {
                    try
                    {
                        var ct = c.ControlType;
                        var name = c.Name ?? "";
                        if (ct == ControlType.Edit && !string.IsNullOrEmpty(name))
                        {
                            var label = FindLabelNear(dialog, c);
                            try { fields.Add(new { type = "text", label, value = c.Patterns.Value.Pattern?.Value, name }); }
                            catch { fields.Add(new { type = "text", label, value = "", name }); }
                        }
                        else if (ct == ControlType.CheckBox && !string.IsNullOrEmpty(name))
                        {
                            fields.Add(new { type = "checkbox", label = name, @checked = GetIsChecked(c) });
                        }
                        else if (ct == ControlType.ComboBox && !string.IsNullOrEmpty(name))
                        {
                            string? current = null;
                            try
                            {
                                if (c.Patterns.Value.Pattern != null)
                                    current = c.Patterns.Value.Pattern.Value;
                                else if (c.Patterns.ExpandCollapse.Pattern != null)
                                {
                                    var child = SafeGetChildren(c, 2000).FirstOrDefault();
                                    if (child != null) current = child.Name;
                                }
                            }
                            catch { }
                            fields.Add(new { type = "combobox", label = name, value = current ?? "" });
                        }
                        else if (ct == ControlType.Table || ct == ControlType.DataGrid)
                        {
                            var columns = new List<string>();
                            var rows = new List<List<string>>();
                            try
                            {
                                foreach (var header in SafeGetChildren(c, 2000))
                                {
                                    try
                                    {
                                        if (header.ControlType == ControlType.Header || header.ControlType == ControlType.HeaderItem)
                                        {
                                            foreach (var hi in SafeGetChildren(header, 1000))
                                            {
                                                try { if (!string.IsNullOrEmpty(hi.Name)) columns.Add(hi.Name); } catch { }
                                            }
                                        }
                                    }
                                    catch { }
                                }
                                int rowCount = 0;
                                foreach (var row in SafeGetChildren(c, 3000))
                                {
                                    try
                                    {
                                        if (row.ControlType == ControlType.DataItem)
                                        {
                                            if (rowCount < 5)
                                            {
                                                var cells = new List<string>();
                                                foreach (var cell in SafeGetChildren(row, 1000))
                                                {
                                                    try { cells.Add(cell.Name ?? ""); } catch { cells.Add(""); }
                                                }
                                                rows.Add(cells);
                                            }
                                            rowCount++;
                                        }
                                    }
                                    catch { }
                                }
                                fields.Add(new { type = "datagrid", name, columns, rowsShown = rows.Count, totalRows = rowCount, sampleRows = rows });
                            }
                            catch { }
                        }
                        else
                        {
                            stack.Enqueue((c, depth + 1));
                        }
                    }
                    catch { }
                }
            }
        }
        catch { }
        return fields;
    }

    private static List<string> ReadTabs(AutomationElement dialog)
    {
        var tabs = new List<string>();
        try
        {
            foreach (var c in SafeGetChildren(dialog, 3000))
            {
                try
                {
                    if (c.ControlType == ControlType.Tab)
                    {
                        foreach (var tab in SafeGetChildren(c, 3000))
                        {
                            try
                            {
                                if (tab.ControlType == ControlType.TabItem && !string.IsNullOrEmpty(tab.Name))
                                    tabs.Add(tab.Name);
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return tabs;
    }
}
