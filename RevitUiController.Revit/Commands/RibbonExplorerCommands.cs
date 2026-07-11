using UiController.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Revit.Commands;

public class RibbonTabsCommand : ICommand
{
    public string Name => "ribbon-tabs";
    public string Description => "List ribbon tabs and their buttons";
    public string Usage => "ribbon-tabs (rt) [tab-name]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var rootChildren = SafeGetChildren(window, 25000);
        var ribbonList = FindRibbonList(rootChildren);
        if (ribbonList == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "ribbon list", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var ribbonTabs = new List<(string name, AutomationElement dataItem)>();
        foreach (var item in SafeGetChildren(ribbonList, 5000))
        {
            try
            {
                if (item.ControlType == ControlType.DataItem)
                    ribbonTabs.Add((GetRibbonTabName(item), item));
            }
            catch { }
        }

        if (args.Length == 0)
        {
            var tabs = ribbonTabs.Select(t => new
            {
                name = t.name,
                automationId = Truncate(t.dataItem.AutomationId ?? "", 30),
                visible = t.dataItem.BoundingRectangle.Width > 10
            }).ToList();

            var result = new CommandResult
            {
                Command = "ribbon-tabs",
                Success = true,
                Data = CoreSettings.Verbosity == "minimal"
                    ? new { count = tabs.Count }
                    : new { tabs, count = tabs.Count }
            };
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return 0;
        }

        var searchName = string.Join(" ", args);
        foreach (var (name, item) in ribbonTabs)
        {
            if (name.Contains(searchName, StringComparison.OrdinalIgnoreCase))
            {
                var before = OutputFormatter.CaptureState(window);
                TryClick(item, name);
                await Task.Delay(500, ct);

                var buttons = new List<(string name, string autoId)>();
                try
                {
                    foreach (var c in SafeGetChildren(item, 3000))
                        CollectButtons(c, buttons, 6);
                }
                catch { }

                var after = OutputFormatter.CaptureState(window);
                var diff = OutputFormatter.ComputeDiff(before, after);

                var result = new CommandResult
                {
                    Command = "ribbon-tabs",
                    Success = true,
                    Diff = diff,
                    Data = CoreSettings.Verbosity == "minimal"
                        ? new { tab = name }
                        : new { tab = name, buttons = buttons.Select(b => new { b.name, automationId = Truncate(b.autoId, 25) }).ToList(), buttonCount = buttons.Count }
                };
                Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
                return 0;
            }
        }

        Console.Write(OutputFormatter.FormatError("NotFound", searchName, ribbonTabs.Select(t => t.name).ToList(), CoreSettings.GlobalOptions));
        return 1;
    }

    private static AutomationElement? FindRibbonList(AutomationElement[] rootChildren)
    {
        foreach (var c in rootChildren)
        {
            try
            {
                if (c.ControlType == ControlType.List)
                {
                    var children = SafeGetChildren(c, 5000);
                    foreach (var child in children)
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
    }

    private static string GetRibbonTabName(AutomationElement dataItem)
    {
        var autoId = dataItem.AutomationId ?? "";
        if (!string.IsNullOrEmpty(autoId) && !autoId.StartsWith("UIFramework") && !autoId.StartsWith("Autodesk"))
            return autoId;
        try
        {
            foreach (var c in SafeGetChildren(dataItem, 3000))
            {
                try { if (c.ControlType == ControlType.Custom && !string.IsNullOrEmpty(c.Name)) return c.Name; } catch { }
                try
                {
                    foreach (var cc in SafeGetChildren(c, 3000))
                    {
                        try { if (!string.IsNullOrEmpty(cc.Name) && !cc.Name.StartsWith("UIFramework") && !cc.Name.StartsWith("Autodesk")) return cc.Name; } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return dataItem.Name;
    }

    private static void CollectButtons(AutomationElement element, List<(string, string)> result, int depth)
    {
        if (depth <= 0) return;
        try
        {
            foreach (var c in SafeGetChildren(element, 3000))
            {
                try
                {
                    var name = c.Name ?? "";
                    var autoId = c.AutomationId ?? "";
                    if (c.ControlType == ControlType.Button &&
                        !string.IsNullOrEmpty(name) &&
                        !name.StartsWith("UIFramework") &&
                        !name.StartsWith("Autodesk"))
                        result.Add((name, autoId));
                    CollectButtons(c, result, depth - 1);
                }
                catch { }
            }
        }
        catch { }
    }
}

public class RibbonButtonsCommand : ICommand
{
    public string Name => "rb";
    public string Description => "List all ribbon tabs, panels, and buttons (full tree)";
    public string Usage => "rb [tab-name]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var rootChildren = SafeGetChildren(window, 40000);
        if (rootChildren.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "UI tree", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var mMainTabs = FindChildByAutoId(rootChildren, "mMainTabs");
        if (mMainTabs == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "mMainTabs", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var tabButtons = GetTabButtons(mMainTabs);
        if (tabButtons.Count == 0)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "ribbon tabs", null, CoreSettings.GlobalOptions));
            return 1;
        }

        if (args.Length > 0)
        {
            var filter = string.Join(" ", args).ToLowerInvariant();
            tabButtons = tabButtons.Where(t => t.name.ToLowerInvariant().Contains(filter)).ToList();
        }

        var tabsData = new List<object>();
        foreach (var (tabName, tabBtn) in tabButtons)
        {
            TryClick(tabBtn, tabName);
            await Task.Delay(1000, ct);

            var freshRoot = SafeGetChildren(window, 20000);
            var freshList = FindRibbonListFast(freshRoot);
            var panels = freshList != null ? ReadActivePanels(freshList) : [];

            var panelsData = panels.Select(p => new
            {
                panel = p.panelName,
                buttons = p.buttons.Select(b => new { name = b.btnName.Replace("\n", " ").Replace("\r", ""), automationId = b.btnId }).ToList()
            }).ToList();

            tabsData.Add(new { tab = tabName, panels = panelsData });
        }

        var result = new CommandResult
        {
            Command = "rb",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { count = tabsData.Count }
                : new { tabs = tabsData, totalTabs = tabButtons.Count }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }

    private static List<(string name, AutomationElement btn)> GetTabButtons(AutomationElement mMainTabs)
    {
        var result = new List<(string, AutomationElement)>();
        foreach (var c in SafeGetChildren(mMainTabs, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.Button)
                {
                    var name = c.Name ?? "";
                    if (!string.IsNullOrEmpty(name))
                        result.Add((name, c));
                }
            }
            catch { }
        }
        return result;
    }

    private static AutomationElement? FindChildByAutoId(AutomationElement[] children, string autoId)
    {
        foreach (var c in children)
        {
            try { if (SafeGetAutoId(c) == autoId) return c; } catch { }
        }
        return null;
    }

    private static AutomationElement? FindRibbonListFast(AutomationElement[] rootChildren)
    {
        foreach (var c in rootChildren)
        {
            try
            {
                if (c.ControlType == ControlType.List)
                {
                    var children = SafeGetChildren(c, 3000);
                    foreach (var child in children)
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
    }

    private static List<(string panelName, List<(string btnName, string btnId)> buttons)> ReadActivePanels(AutomationElement ribbonList)
    {
        var result = new List<(string, List<(string, string)>)>();

        AutomationElement? activeDataItem = null;
        foreach (var item in SafeGetChildren(ribbonList, 3000))
        {
            try
            {
                if (item.ControlType == ControlType.DataItem)
                {
                    var rect = item.BoundingRectangle;
                    if (rect.Width > 10) { activeDataItem = item; break; }
                }
            }
            catch { }
        }
        if (activeDataItem == null) return result;

        AutomationElement? panelsList = null;
        try
        {
            foreach (var c1 in SafeGetChildren(activeDataItem, 3000))
            {
                try
                {
                    foreach (var c2 in SafeGetChildren(c1, 3000))
                    {
                        try { if (SafeGetAutoId(c2) == "mMainTabPanels") { panelsList = c2; break; } } catch { }
                    }
                    if (panelsList != null) break;
                }
                catch { }
            }
        }
        catch { }
        if (panelsList == null) return result;

        foreach (var panel in SafeGetChildren(panelsList, 5000))
        {
            try
            {
                if (panel.ControlType != ControlType.DataItem) continue;

                var panelName = ExtractPanelName(panel);
                var buttons = new List<(string, string)>();

                var buttonView = FindButtonView(panel);
                if (buttonView != null)
                {
                    foreach (var btn in SafeGetChildren(buttonView, 3000))
                    {
                        try
                        {
                            if (btn.ControlType == ControlType.DataItem)
                            {
                                var label = ExtractButtonLabel(btn);
                                var id = SafeGetAutoId(btn);
                                if (!string.IsNullOrEmpty(label) && !label.StartsWith("Autodesk") && !label.StartsWith("UIFramework"))
                                    buttons.Add((label, id));
                            }
                        }
                        catch { }
                    }
                }

                result.Add((panelName, buttons));
            }
            catch { }
        }

        return result;
    }

    private static string ExtractPanelName(AutomationElement panelDataItem)
    {
        try
        {
            foreach (var c in SafeGetChildren(panelDataItem, 3000))
            {
                try
                {
                    var n = c.Name ?? "";
                    if (c.ControlType == ControlType.Custom && !string.IsNullOrEmpty(n) && !n.StartsWith("UIFramework"))
                        return n;
                    foreach (var cc in SafeGetChildren(c, 3000))
                    {
                        try
                        {
                            var nn = cc.Name ?? "";
                            if (!string.IsNullOrEmpty(nn) && !nn.StartsWith("UIFramework") && !nn.StartsWith("Autodesk"))
                                return nn;
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return "?";
    }

    private static AutomationElement? FindButtonView(AutomationElement panelDataItem)
    {
        try
        {
            foreach (var c in SafeGetChildren(panelDataItem, 3000))
            {
                try
                {
                    foreach (var cc in SafeGetChildren(c, 3000))
                    {
                        try { if (SafeGetAutoId(cc) == "mRibbonPanelView") return cc; } catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    private static string ExtractButtonLabel(AutomationElement buttonDataItem)
    {
        try
        {
            var name = buttonDataItem.Name ?? "";
            if (!string.IsNullOrEmpty(name) && !name.StartsWith("Autodesk") && !name.StartsWith("UIFramework"))
                return name;
            foreach (var c in SafeGetChildren(buttonDataItem, 3000))
            {
                try
                {
                    var n = c.Name ?? "";
                    if (!string.IsNullOrEmpty(n) && !n.StartsWith("Autodesk") && !n.StartsWith("UIFramework"))
                        return n;
                    foreach (var cc in SafeGetChildren(c, 3000))
                    {
                        try
                        {
                            var nn = cc.Name ?? "";
                            if (!string.IsNullOrEmpty(nn) && !nn.StartsWith("Autodesk") && !nn.StartsWith("UIFramework"))
                                return nn;
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return "";
    }
}
