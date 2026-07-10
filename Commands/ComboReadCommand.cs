using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class ComboReadCommand : ICommand
{
    public string Name => "combo-read";
    public string Description => "Open a ComboBox, read all items via SelectionPattern, then close. Usage: combo-read <name>";
    public string Usage => "combo-read <name>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var name = string.Join(" ", args);
        if (string.IsNullOrEmpty(name))
        {
            Console.Error.WriteLine("Usage: combo-read <name>");
            return 1;
        }

        var element = AutomationHelper.FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.GlobalOptions));
            return 1;
        }

        var items = new List<object>();
        string? selectedValue = null;

        try
        {
            var expandCollapse = element.Patterns.ExpandCollapse.Pattern;
            var selection = element.Patterns.Selection.Pattern;
            var value = element.Patterns.Value.Pattern;

            selectedValue = value?.Value;

            if (expandCollapse != null)
            {
                expandCollapse.Expand();
                await Task.Delay(200, ct);

                var listItems = element.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                foreach (var li in listItems)
                {
                    try
                    {
                        items.Add(new
                        {
                            name = li.Name ?? "",
                            automationId = SafeGet(() => li.AutomationId),
                            isSelected = li.Patterns.SelectionItem.Pattern?.IsSelected ?? false,
                            enabled = li.IsEnabled
                        });
                    }
                    catch { }
                }

                expandCollapse.Collapse();
            }
            else
            {
                var listItems = element.FindAllChildren(cf => cf.ByControlType(ControlType.ListItem));
                foreach (var li in listItems)
                {
                    try
                    {
                        items.Add(new
                        {
                            name = li.Name ?? "",
                            automationId = SafeGet(() => li.AutomationId),
                            isSelected = li.Patterns.SelectionItem.Pattern?.IsSelected ?? false,
                            enabled = li.IsEnabled
                        });
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ComboRead warning: {ex.Message}");
        }

        var result = new CommandResult
        {
            Command = "combo-read",
            Success = true,
            Data = new
            {
                element = new
                {
                    name = SafeGet(() => element.Name),
                    controlType = SafeGet(() => element.ControlType.ToString()),
                    automationId = SafeGet(() => element.AutomationId)
                },
                selectedValue,
                itemCount = items.Count,
                items
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return 0;
    }

    private static string SafeGet(Func<object?> f) { try { return f()?.ToString() ?? ""; } catch { return ""; } }
}
