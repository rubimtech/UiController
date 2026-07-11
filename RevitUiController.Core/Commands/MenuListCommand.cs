using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class MenuListCommand : ICommand
{
    public string Name => "menu-list";
    public string Description => "Enumerate menu bar items. Usage: menu-list";
    public string Usage => "menu-list";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        AutomationElement? menuBar = FindFirstChildByType(window, ControlType.MenuBar);
        menuBar ??= FindFirstChildByType(window, ControlType.Menu);

        if (menuBar == null)
        {
            Console.Write(OutputFormatter.FormatError("MenuNotFound", "", ["No MenuBar or Menu found"], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var items = new List<object>();
        foreach (var c in SafeGetChildren(menuBar, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.MenuItem)
                {
                    items.Add(new
                    {
                        name = c.Name ?? "",
                        enabled = c.IsEnabled,
                        visible = c.IsOffscreen == false,
                        childrenCount = CountSubMenuItems(c)
                    });
                }
            }
            catch { }
        }

        var result = new CommandResult
        {
            Command = "menu-list",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { count = items.Count }
                : new { count = items.Count, items }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }

    private static int CountSubMenuItems(AutomationElement menuItem)
    {
        try
        {
            return SafeGetChildren(menuItem, 2000).Count(c =>
            {
                try { return c.ControlType == ControlType.MenuItem; }
                catch { return false; }
            });
        }
        catch { return 0; }
    }
}
