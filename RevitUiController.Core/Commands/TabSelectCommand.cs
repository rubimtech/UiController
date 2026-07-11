using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class TabSelectCommand : ICommand
{
    public string Name => "tab-select";
    public string Description => "Select a tab by name. Usage: tab-select <name>";
    public string Usage => "tab-select <name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "tab-select <name>", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var tabName = string.Join(" ", args);

        AutomationElement? tab = FindFirstChildByType(window, ControlType.Tab);
        if (tab == null)
        {
            Console.Write(OutputFormatter.FormatError("TabNotFound", tabName, ["No Tab control found"], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        AutomationElement? tabItem = null;
        foreach (var c in SafeGetChildren(tab, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.TabItem &&
                    (c.Name ?? "").Contains(tabName, StringComparison.OrdinalIgnoreCase))
                {
                    tabItem = c;
                    break;
                }
            }
            catch { }
        }

        if (tabItem == null)
        {
            Console.Write(OutputFormatter.FormatError("TabItemNotFound", tabName, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        if (!TryClick(tabItem, tabName))
        {
            Console.Write(OutputFormatter.FormatError("ClickFailed", tabName, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var result = new CommandResult
        {
            Command = "tab-select",
            Success = true,
            Data = new { tab = tabName }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}
