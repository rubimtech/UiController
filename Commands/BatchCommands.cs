using System.Text.Json;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

namespace RevitUiController.Commands;

public class PropertySheetBatchCommand : ICommand
{
    public string Name => "ps-batch";
    public string Description => "Fill multiple fields in a PropertySheet dialog from JSON";
    public string Usage => "ps-batch <dialog-title> <json-payload> [--tab <tab-name>] [--timeout <sec>]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: ps-batch <dialog-title> <json-payload> [--tab <tab-name>] [--timeout <sec>]");
            return 1;
        }

        var dialogTitle = args[0];
        var jsonPayload = args[1];
        string? targetTab = null;
        var timeoutSeconds = 60;

        for (int i = 2; i < args.Length; i++)
        {
            if (args[i] == "--tab" && i + 1 < args.Length)
                targetTab = args[++i];
            else if (args[i] == "--timeout" && i + 1 < args.Length && int.TryParse(args[++i], out var t))
                timeoutSeconds = t;
        }

        var dialog = FindActiveDialog(revitWindow, dialogTitle);
        if (dialog == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps-batch",
                Success = false,
                Error = $"Dialog '{dialogTitle}' not found"
            }, Program.IsPretty));
            return 1;
        }

        Dictionary<string, JsonElement>? fields;
        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonPayload);
            if (parsed == null || parsed.Count == 0)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps-batch",
                    Success = false,
                    Error = "JSON payload is empty or invalid"
                }, Program.IsPretty));
                return 1;
            }
            fields = parsed;
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ps-batch",
                Success = false,
                Error = $"Failed to parse JSON payload: {ex.Message}"
            }, Program.IsPretty));
            return 1;
        }

        if (targetTab != null)
        {
            var tabClicked = TrySelectTab(dialog, targetTab);
            if (!tabClicked)
            {
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "ps-batch",
                    Success = false,
                    Error = $"Tab '{targetTab}' not found in dialog"
                }, Program.IsPretty));
                return 1;
            }
        }

        var filledFields = new Dictionary<string, object?>();
        var failures = new List<string>();
        int total = fields.Count;

        foreach (var kvp in fields)
        {
            var label = kvp.Key;
            var value = kvp.Value;

            try
            {
                if (value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
                {
                    var result = SetCheckboxField(dialog, label, value.GetBoolean());
                    if (result)
                        filledFields[label] = value.GetBoolean();
                    else
                        failures.Add($"Field '{label}': checkbox not found");
                }
                else if (value.ValueKind == JsonValueKind.String)
                {
                    var text = value.GetString() ?? "";
                    var result = SetTextField(dialog, label, text);
                    if (result != null)
                        filledFields[label] = text;
                    else
                        failures.Add($"Field '{label}': {result}");
                }
                else
                {
                    failures.Add($"Field '{label}': unsupported value type '{value.ValueKind}'");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"Field '{label}': {ex.Message}");
            }
        }

        var progressDeadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        bool progressWasVisible = false;
        while (DateTime.UtcNow < progressDeadline)
        {
            var bar = FindProgressBar(revitWindow);
            if (bar == null)
            {
                if (progressWasVisible)
                    break;
                await Task.Delay(500, ct);
                continue;
            }
            progressWasVisible = true;
            try
            {
                var val = bar.Patterns.RangeValue.Pattern?.Value;
                if (val >= 100)
                    break;
            }
            catch { }
            await Task.Delay(500, ct);
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "ps-batch",
            Success = failures.Count == 0,
            Error = failures.Count > 0 ? $"Failed to fill {failures.Count} field(s)" : null,
            Data = new
            {
                dialog = dialog.Name,
                total,
                filled = filledFields.Count,
                failed = failures.Count,
                failures,
                fields = filledFields
            }
        }, Program.IsPretty));

        return failures.Count == 0 ? 0 : 1;
    }

    private static AutomationElement? FindActiveDialog(AutomationElement root, string name)
    {
        var dialogs = FindActiveDialogs(root);
        return dialogs.FirstOrDefault(d =>
            (d.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TrySelectTab(AutomationElement dialog, string tabName)
    {
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Tab)
                    {
                        foreach (var tab in SafeGetChildren(c, 3000))
                        {
                            try
                            {
                                if (tab.ControlType == ControlType.TabItem &&
                                    (tab.Name ?? "").Contains(tabName, StringComparison.OrdinalIgnoreCase))
                                {
                                    tab.Click();
                                    Thread.Sleep(200);
                                    return true;
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private static bool SetCheckboxField(AutomationElement dialog, string label, bool desiredState)
    {
        var checkbox = FindCheckboxByLabel(dialog, label);
        if (checkbox == null)
            return false;

        var currentState = GetIsChecked(checkbox);
        if (currentState != desiredState)
        {
            checkbox.Click();
            Thread.Sleep(100);
        }
        return true;
    }

    private static string? SetTextField(AutomationElement dialog, string label, string value)
    {
        var field = FindFieldByLabel(dialog, label);
        if (field == null)
        {
            var combo = FindComboByLabel(dialog, label);
            if (combo != null)
            {
                combo.Click();
                Thread.Sleep(300);
                var item = FindDropdownItem(combo, value);
                if (item != null)
                {
                    item.Click();
                    Thread.Sleep(100);
                    return "ok";
                }
                return $"option '{value}' not found in combobox '{label}'";
            }
            return "field not found";
        }

        if (field.ControlType == ControlType.ComboBox)
        {
            field.Click();
            Thread.Sleep(300);
            var item = FindDropdownItem(field, value);
            if (item != null)
            {
                item.Click();
                Thread.Sleep(100);
                return "ok";
            }
            return $"option '{value}' not found in combobox '{label}'";
        }

        field.Focus();
        field.Click();
        Thread.Sleep(100);
        SendTextSafe(field, value);
        return "ok";
    }

    private static AutomationElement? FindProgressBar(AutomationElement root)
    {
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(root);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    if (el.ControlType == ControlType.ProgressBar)
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
