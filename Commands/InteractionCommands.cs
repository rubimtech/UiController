using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

namespace RevitUiController.Commands;

public class ClickCommand : ICommand
{
    public string Name => "click";
    public string Description => "Click a button/control by name";
    public string Usage => "click <button-name> or click-id <automation-id>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var query = string.Join(" ", args);
        var start = DateTime.UtcNow;

        var found = FindByAutoIdInRoot(revitWindow, query);
        if (found == null)
            found = FindFirstEnabledVisible(revitWindow, query);

        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", query, null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        var clicked = TryClick(found, query);
        if (!clicked)
        {
            Console.Write(OutputFormatter.FormatError("ClickFailed", query, null, Program.IsPretty));
            return Task.FromResult(1);
        }
        var after = OutputFormatter.CaptureState(revitWindow);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
        var result = new CommandResult
        {
            Command = "click",
            Success = true,
            Diff = diff,
            Data = Program.Verbosity == "minimal" ? null : new { target = query },
            DurationMs = elapsed
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }

    private static AutomationElement? FindByAutoIdInRoot(AutomationElement revitWindow, string nameOrId)
    {
        var children = SafeGetChildren(revitWindow, 60000);

        AutomationElement? mMainTabs = null;
        foreach (var c in children)
        {
            try
            {
                var autoId = SafeGetAutoId(c);
                if (autoId.Equals(nameOrId, StringComparison.OrdinalIgnoreCase))
                    return c;
                if (autoId == "mMainTabs")
                    mMainTabs = c;
            }
            catch { }
        }

        if (mMainTabs != null)
        {
            foreach (var cc in SafeGetChildren(mMainTabs, 10000))
            {
                var ccName = SafeGetName(cc);
                var ccId = SafeGetAutoId(cc);
                if (ccName.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
                    ccId.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
                    ccName.Contains(nameOrId, StringComparison.OrdinalIgnoreCase))
                {
                    if (cc.IsEnabled && cc.IsOffscreen == false)
                        return cc;
                }
            }
        }
        return null;
    }
}

public class RibbonCommand : ICommand
{
    public string Name => "ribbon";
    public string Description => "Click a ribbon button, optionally after switching to a tab";
    public string Usage => "ribbon <button-name> [tab-name]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var start = DateTime.UtcNow;

        if (args.Length >= 2)
        {
            var tabName = args[^1];
            var searchName = string.Join(" ", args.Take(args.Length - 1));

            var tab = FindFirstEnabledVisible(revitWindow, tabName);
            if (tab == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", $"tab '{tabName}'", null, Program.IsPretty));
                return Task.FromResult(1);
            }

            var before = OutputFormatter.CaptureState(revitWindow);
            TryClick(tab, tabName);
            Thread.Sleep(300);

            var button = FindFirstEnabledVisible(revitWindow, searchName);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", searchName, null, Program.IsPretty));
                return Task.FromResult(1);
            }
            TryClick(button, searchName);

            var after = OutputFormatter.CaptureState(revitWindow);
            var diff = OutputFormatter.ComputeDiff(before, after);

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var result = new CommandResult
            {
                Command = "ribbon",
                Success = true,
                Diff = diff,
                Data = Program.Verbosity == "minimal" ? null : new { button = searchName, tab = tabName },
                DurationMs = elapsed
            };
            if (Program.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
            Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        }
        else
        {
            var name = args[0];
            var button = FindFirstEnabledVisible(revitWindow, name);
            if (button == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.IsPretty));
                return Task.FromResult(1);
            }

            var before = OutputFormatter.CaptureState(revitWindow);
            TryClick(button, name);
            var after = OutputFormatter.CaptureState(revitWindow);
            var diff = OutputFormatter.ComputeDiff(before, after);

            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var result = new CommandResult
            {
                Command = "ribbon",
                Success = true,
                Diff = diff,
                Data = Program.Verbosity == "minimal" ? null : new { button = name },
                DurationMs = elapsed
            };
            if (Program.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
            Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        }
        return Task.FromResult(0);
    }
}

public class SwitchViewCommand : ICommand
{
    public string Name => "switch-view";
    public string Description => "Switch to a view tab or list view tabs (use sv without args to list all)";
    public string Usage => "switch-view (sv) [view-name]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
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
            Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
            return Task.FromResult(0);
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
                Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
                element.Click();
                return Task.FromResult(0);
            }
        }

        var tabList = tabs.Select(t => t.name).ToList();
        Console.Write(OutputFormatter.FormatError("NotFound", viewName, tabList, Program.IsPretty));
        return Task.FromResult(1);
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

public class TypeTextCommand : ICommand
{
    public string Name => "type";
    public string Description => "Type text into a control";
    public string Usage => "type <control-name> <text>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length < 2)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "type <control-name> <text>", null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var controlName = args[0];
        var text = string.Join(" ", args.Skip(1));

        var found = FindFirstEnabledVisible(revitWindow, controlName);
        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", controlName, null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        found.Focus();
        found.Click();
        Thread.Sleep(200);
        SendTextSafe(found, text);
        var after = OutputFormatter.CaptureState(revitWindow);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var result = new CommandResult
        {
            Command = "type",
            Success = true,
            Diff = diff,
            Data = Program.Verbosity == "minimal"
                ? new { control = controlName }
                : new { control = controlName, text, targetName = found.Name ?? "" }
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class RibbonTabsCommand : ICommand
{
    public string Name => "ribbon-tabs";
    public string Description => "List ribbon tabs and their buttons";
    public string Usage => "ribbon-tabs (rt) [tab-name]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var rootChildren = SafeGetChildren(revitWindow, 25000);
        var ribbonList = FindRibbonList(rootChildren);
        if (ribbonList == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "ribbon list", null, Program.IsPretty));
            return Task.FromResult(1);
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
                Data = Program.Verbosity == "minimal"
                    ? new { count = tabs.Count }
                    : new { tabs, count = tabs.Count }
            };
            Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
            return Task.FromResult(0);
        }

        var searchName = string.Join(" ", args);
        foreach (var (name, item) in ribbonTabs)
        {
            if (name.Contains(searchName, StringComparison.OrdinalIgnoreCase))
            {
                var before = OutputFormatter.CaptureState(revitWindow);
                TryClick(item, name);
                Thread.Sleep(500);

                var buttons = new List<(string name, string autoId)>();
                try
                {
                    foreach (var c in SafeGetChildren(item, 3000))
                        CollectButtons(c, buttons, 6);
                }
                catch { }

                var after = OutputFormatter.CaptureState(revitWindow);
                var diff = OutputFormatter.ComputeDiff(before, after);

                var result = new CommandResult
                {
                    Command = "ribbon-tabs",
                    Success = true,
                    Diff = diff,
                    Data = Program.Verbosity == "minimal"
                        ? new { tab = name }
                        : new { tab = name, buttons = buttons.Select(b => new { b.name, automationId = Truncate(b.autoId, 25) }).ToList(), buttonCount = buttons.Count }
                };
                Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
                return Task.FromResult(0);
            }
        }

        Console.Write(OutputFormatter.FormatError("NotFound", searchName, ribbonTabs.Select(t => t.name).ToList(), Program.IsPretty));
        return Task.FromResult(1);
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

public class ExpandCommand : ICommand
{
    public string Name => "expand";
    public string Description => "Expand/collapse details buttons in active dialogs";
    public string Usage => "expand";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var dialogs = FindActiveDialogs(revitWindow);
        if (dialogs.Count == 0)
        {
            var result = new CommandResult
            {
                Command = "expand",
                Success = true,
                Data = new { dialogsScanned = 0, buttonsClicked = 0 }
            };
            Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
            return Task.FromResult(0);
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        var clicked = 0;
        foreach (var dialog in dialogs)
        {
            var buttons = FindControlsByName(dialog, "Подробности", maxResults: 20);
            var more = FindControlsByName(dialog, "Details", maxResults: 20);
            buttons.AddRange(more.Where(b => !buttons.Any(x => x.Name == b.Name)));

            foreach (var b in buttons)
            {
                if (b.IsEnabled && TryClick(b, b.Name))
                {
                    clicked++;
                    Thread.Sleep(500);
                }
            }
        }
        var after = OutputFormatter.CaptureState(revitWindow);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var result2 = new CommandResult
        {
            Command = "expand",
            Success = true,
            Diff = diff,
            Data = new { dialogsScanned = dialogs.Count, buttonsClicked = clicked }
        };
        Console.Write(OutputFormatter.FormatResult(result2, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class RibbonButtonsCommand : ICommand
{
    public string Name => "rb";
    public string Description => "List all ribbon tabs, panels, and buttons (full tree)";
    public string Usage => "rb [tab-name]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var rootChildren = SafeGetChildren(revitWindow, 40000);
        if (rootChildren.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "UI tree", null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var mMainTabs = FindChildByAutoId(rootChildren, "mMainTabs");
        if (mMainTabs == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "mMainTabs", null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var tabButtons = GetTabButtons(mMainTabs);
        if (tabButtons.Count == 0)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "ribbon tabs", null, Program.IsPretty));
            return Task.FromResult(1);
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
            Thread.Sleep(1000);

            var freshRoot = SafeGetChildren(revitWindow, 20000);
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
            Data = Program.Verbosity == "minimal"
                ? new { count = tabsData.Count }
                : new { tabs = tabsData, totalTabs = tabButtons.Count }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
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
