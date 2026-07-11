using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class ListItemsCommand : ICommand
{
    public string Name => "list-items";
    public string Description => "Read all items in a ListBox/ListView via SelectionPattern. Usage: list-items <name> [--max N]";
    public string Usage => "list-items <name> [--max N]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("ListItemsCommand", "Usage: list-items <name> [--max N]");
            return Task.FromResult(1);
        }

        var maxItems = 200;
        var name = "";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--max" && i + 1 < args.Length) int.TryParse(args[++i], out maxItems);
            else name = (name == "" ? args[i] : name + " " + args[i]);
        }

        var element = AutomationHelper.FindFirstEnabledVisible(window, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var items = new List<object>();
        int selectedCount = 0;

        try
        {
            var selection = element.Patterns.Selection.Pattern;
            var selected = selection?.Selection?.ValueOrDefault;
            selectedCount = selected?.Length ?? 0;

            var listItems = element.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));

            foreach (var li in listItems)
            {
                if (items.Count >= maxItems) break;
                try
                {
                    var entry = new Dictionary<string, object?>
                    {
                        ["name"] = li.Name ?? "",
                        ["index"] = items.Count,
                        ["automationId"] = SafeGet(() => li.AutomationId),
                        ["enabled"] = SafeGet(() => li.IsEnabled),
                        ["isSelected"] = false
                    };

                    try
                    {
                        entry["isSelected"] = li.Patterns.SelectionItem.Pattern?.IsSelected ?? false;
                    }
                    catch { }

                    try
                    {
                        var toggle = li.Patterns.Toggle.Pattern;
                        if (toggle != null)
                            entry["checkState"] = toggle.ToggleState.ToString();
                    }
                    catch { }

                    items.Add(entry);
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            LoggingService.Warn("ListItemsCommand", $"ListItems warning: {ex.Message}");
        }

        var result = new CommandResult
        {
            Command = "list-items",
            Success = true,
            Data = new
            {
                element = new
                {
                    name = SafeGet(() => element.Name),
                    controlType = SafeGet(() => element.ControlType.ToString()),
                    automationId = SafeGet(() => element.AutomationId)
                },
                itemCount = items.Count,
                selectedCount,
                truncated = items.Count >= maxItems,
                items
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }

    private static string SafeGet(Func<object?> f) { try { return f()?.ToString() ?? ""; } catch { return ""; } }
}
