using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Commands;

public class SwitchViewCommand : ICommand
{
    public string Name => "switch-view";
    public string Description => "Switch to a view tab or list view tabs (use sv without args to list all)";
    public string Usage => "switch-view (sv) [view-name]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var tabs = new List<(string name, AutomationElement element)>();
        var rootChildren = SafeGetChildren(revitWindow, 10000);

        foreach (var c in rootChildren)
        {
            try
            {
                if (c.ControlType != ControlType.Tab)
                {
                    foreach (var deep in c.FindAllChildren()) { if (deep.ControlType == ControlType.Tab) { tabs.AddRange(GetTabs(deep)); } }
                    continue;
                }
                tabs.AddRange(GetTabs(c));
            }
            catch { }
        }

        if (args.Length == 0)
        {
            var tabNames = tabs.Select(t => t.name).ToList();
            var result = new CommandResult
            {
                Command = "switch-view",
                Success = true,
                Data = Program.Verbosity == "minimal"
                    ? new { count = tabNames.Count }
                    : new { tabs = tabNames, count = tabNames.Count }
            };
            Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
            return 0;
        }

        var viewName = string.Join(" ", args);
        foreach (var (name, element) in tabs)
        {
            if (name.Contains(viewName, StringComparison.OrdinalIgnoreCase) && element.IsEnabled && element.IsOffscreen == false)
            {
                var tabNames = tabs.Select(t => t.name).ToList();
                var result = new CommandResult
                {
                    Command = "switch-view",
                    Success = true,
                    Data = Program.Verbosity == "minimal"
                        ? new { view = name }
                        : new { view = name, tabs = tabNames, count = tabNames.Count }
                };
                Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
                element.Click();
                return 0;
            }
        }

        var tabList = tabs.Select(t => t.name).ToList();
        Console.Write(OutputFormatter.FormatError("NotFound", viewName, tabList, Program.GlobalOptions));
        return 1;
    }

    private static string GetTabDisplayName(AutomationElement tab)
    {
        var name = tab.Name ?? "";
        if (!string.IsNullOrEmpty(name) && !name.StartsWith("Xceed.")) return name;

        try
        {
            foreach (var c in SafeGetChildren(tab, 3000))
            {
                try
                {
                    var cName = c.Name ?? "";
                    if (!string.IsNullOrEmpty(cName) && !cName.StartsWith("Xceed."))
                        return cName;
                    foreach (var cc in SafeGetChildren(c, 3000))
                    {
                        try
                        {
                            var ccName = cc.Name ?? "";
                            if (!string.IsNullOrEmpty(ccName) && !ccName.StartsWith("Xceed."))
                                return ccName;
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return name;
    }

    private static List<(string name, AutomationElement element)> GetTabs(AutomationElement tabControl)
    {
        var result = new List<(string, AutomationElement)>();
        try
        {
            foreach (var tab in SafeGetChildren(tabControl, 5000))
            {
                try
                {
                    if (tab.ControlType == ControlType.TabItem)
                    {
                        var displayName = GetTabDisplayName(tab);
                        result.Add((displayName, tab));
                    }
                }
                catch { }
            }
        }
        catch { }
        return result;
    }
}
