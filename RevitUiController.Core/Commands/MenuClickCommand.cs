using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class MenuClickCommand : ICommand
{
    public string Name => "menu-click";
    public string Description => "Click a MenuItem by name/path. Usage: menu-click <path>";
    public string Usage => "menu-click <path>";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "menu-click <path>", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var path = string.Join(" ", args);
        var segments = path.Split(['/', '\\', '>'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        AutomationElement? current = FindFirstChildByType(window, ControlType.MenuBar);
        if (current == null)
        {
            current = FindFirstChildByType(window, ControlType.Menu);
            if (current == null)
            {
                Console.Write(OutputFormatter.FormatError("MenuNotFound", path, ["No MenuBar or Menu found"], CoreSettings.GlobalOptions));
                return 1;
            }
        }

        foreach (var segment in segments)
        {
            var menuItem = FindMenuItem(current, segment);
            if (menuItem == null)
            {
                Console.Write(OutputFormatter.FormatError("MenuItemNotFound", segment, null, CoreSettings.GlobalOptions));
                return 1;
            }

            if (segment == segments[^1])
            {
                if (!TryClick(menuItem, segment))
                {
                    Console.Write(OutputFormatter.FormatError("ClickFailed", segment, null, CoreSettings.GlobalOptions));
                    return 1;
                }
                await Task.Delay(200, ct);
            }
            else
            {
                try { menuItem.Click(); }
                catch
                {
                    Console.Write(OutputFormatter.FormatError("ExpandFailed", segment, null, CoreSettings.GlobalOptions));
                    return 1;
                }
                await Task.Delay(300, ct);
            }

            current = menuItem;
        }

        var result = new CommandResult
        {
            Command = "menu-click",
            Success = true,
            Data = new { path, segments = segments.Length }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }

    private static AutomationElement? FindMenuItem(AutomationElement parent, string name)
    {
        foreach (var c in SafeGetChildren(parent, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.MenuItem &&
                    (c.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            catch { }
        }
        return null;
    }
}
