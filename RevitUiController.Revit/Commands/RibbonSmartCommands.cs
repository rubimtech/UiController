using UiController.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Revit.Commands;

public class RibbonFindCommand : ICommand
{
    public string Name => "ribbon-find";
    public string Description => "Find ribbon tab/panel/button by name with exact location";
    public string Usage => "ribbon-find <tab-name> [panel-name [button-name]]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        var rootChildren = SafeGetChildren(window, 40000);
        var mMainTabs = FindChildByAutoId(rootChildren, "mMainTabs");
        if (mMainTabs == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "mMainTabs", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var tabName = args[0];
        var tabResult = FindTab(mMainTabs, tabName);
        if (tabResult == null)
        {
            var suggestions = GetTabNames(mMainTabs);
            Console.Write(OutputFormatter.FormatError("NotFound", $"tab '{tabName}'", suggestions, CoreSettings.GlobalOptions));
            return 1;
        }

        var (tabFoundName, tabFoundElement) = tabResult.Value;
        TryClick(tabFoundElement, tabFoundName);
        await Task.Delay(500, ct);

        if (args.Length == 1)
        {
            var rect = tabFoundElement.BoundingRectangle;
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var result = new CommandResult
            {
                Command = "ribbon-find",
                Success = true,
                Data = new
                {
                    tab = tabFoundName,
                    automationId = tabFoundElement.AutomationId ?? "",
                    enabled = tabFoundElement.IsEnabled,
                    visible = tabFoundElement.IsOffscreen == false,
                    boundingRect = new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height }
                },
                DurationMs = elapsed
            };
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return 0;
        }

        var panelName = args[1];
        var freshRoot = SafeGetChildren(window, 20000);
        var freshMMainTabs = FindChildByAutoId(freshRoot, "mMainTabs");
        var panels = freshMMainTabs != null ? ReadActivePanels(freshMMainTabs) : [];

        var panel = panels.FirstOrDefault(p => p.panelName.Contains(panelName, StringComparison.OrdinalIgnoreCase));
        if (panel.panelName == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", $"panel '{panelName}'", panels.Select(p => p.panelName).ToList(), CoreSettings.GlobalOptions));
            return 1;
        }

        if (args.Length == 2)
        {
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var btnList = panel.buttons.Select(b => new { name = b.btnName, automationId = b.btnId }).ToList();
            var result = new CommandResult
            {
                Command = "ribbon-find",
                Success = true,
                Data = new
                {
                    tab = tabFoundName,
                    panel = panel.panelName,
                    buttons = btnList,
                    buttonCount = btnList.Count
                },
                DurationMs = elapsed
            };
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return 0;
        }

        var buttonName = args[2];
        var matchedBtn = panel.buttons.FirstOrDefault(b => b.btnName.Contains(buttonName, StringComparison.OrdinalIgnoreCase));
        if (matchedBtn.btnName == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", $"button '{buttonName}'", panel.buttons.Select(b => b.btnName).ToList(), CoreSettings.GlobalOptions));
            return 1;
        }

        var elapsed2 = (DateTime.UtcNow - start).TotalMilliseconds;
        var result2 = new CommandResult
        {
            Command = "ribbon-find",
            Success = true,
            Data = new
            {
                tab = tabFoundName,
                panel = panel.panelName,
                button = new { name = matchedBtn.btnName, automationId = matchedBtn.btnId }
            },
            DurationMs = elapsed2
        };
        Console.Write(OutputFormatter.FormatResult(result2, CoreSettings.GlobalOptions));
        return 0;
    }

    private static (string name, AutomationElement element)? FindTab(AutomationElement mMainTabs, string searchName)
    {
        foreach (var c in SafeGetChildren(mMainTabs, 5000))
        {
            try
            {
                if (c.ControlType != ControlType.Button) continue;
                var autoId = SafeGetAutoId(c);
                if (autoId.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                    return (autoId, c);
                var name = c.Name ?? "";
                if (name.Equals(searchName, StringComparison.OrdinalIgnoreCase))
                    return (name, c);
            }
            catch { }
        }

        foreach (var c in SafeGetChildren(mMainTabs, 5000))
        {
            try
            {
                if (c.ControlType != ControlType.Button) continue;
                var name = c.Name ?? "";
                if (name.Contains(searchName, StringComparison.OrdinalIgnoreCase))
                    return (name, c);
            }
            catch { }
        }

        return null;
    }

    private static List<string> GetTabNames(AutomationElement mMainTabs)
    {
        var names = new List<string>();
        foreach (var c in SafeGetChildren(mMainTabs, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.Button)
                {
                    var name = c.Name ?? "";
                    if (!string.IsNullOrEmpty(name))
                        names.Add(name);
                }
            }
            catch { }
        }
        return names;
    }

    private static AutomationElement? FindChildByAutoId(AutomationElement[] children, string autoId)
    {
        foreach (var c in children)
        {
            try { if (SafeGetAutoId(c) == autoId) return c; } catch { }
        }
        return null;
    }

    private static List<(string panelName, List<(string btnName, string btnId)> buttons)> ReadActivePanels(AutomationElement mMainTabs)
    {
        var result = new List<(string, List<(string, string)>)>();

        foreach (var c in SafeGetChildren(mMainTabs, 3000))
        {
            try
            {
                if (c.ControlType == ControlType.Button)
                {
                    var rect = c.BoundingRectangle;
                    if (rect.Width > 10 && c.IsOffscreen == false)
                    {
                        AutomationElement? panelsList = null;
                        foreach (var c1 in SafeGetChildren(c, 3000))
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

                        if (panelsList != null)
                        {
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
                            break;
                        }
                    }
                }
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

public class DropDownCommand : ICommand
{
    public string Name => "dropdown";
    public string Description => "Open a ribbon SplitButton/DropDownButton and select an item";
    public string Usage => "dropdown <button-name> <item-name> [tab-name]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length < 2)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "dropdown <button-name> <item-name> [tab-name]", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var buttonName = args[0];
        var itemName = args[1];
        string? tabName = args.Length >= 3 ? args[2] : null;

        AutomationElement? button;
        if (tabName != null)
        {
            var tab = FindFirstEnabledVisible(window, tabName);
            if (tab == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", $"tab '{tabName}'", null, CoreSettings.GlobalOptions));
                return 1;
            }
            TryClick(tab, tabName);
            await Task.Delay(300, ct);
            button = FindFirstEnabledVisible(window, buttonName);
        }
        else
        {
            button = FindFirstEnabledVisible(window, buttonName);
        }

        if (button == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", $"button '{buttonName}'", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var before = OutputFormatter.CaptureState(window);

        if (!TryClick(button, buttonName))
        {
            Console.Write(OutputFormatter.FormatError("ClickFailed", buttonName, null, CoreSettings.GlobalOptions));
            return 1;
        }

        await Task.Delay(500, ct);

        var dropdownItem = FindFirstEnabledVisible(window, itemName);
        if (dropdownItem == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", $"dropdown item '{itemName}'", null, CoreSettings.GlobalOptions));
            return 1;
        }

        if (!TryClick(dropdownItem, itemName))
        {
            Console.Write(OutputFormatter.FormatError("ClickFailed", itemName, null, CoreSettings.GlobalOptions));
            return 1;
        }

        var after = OutputFormatter.CaptureState(window);
        var diff = OutputFormatter.ComputeDiff(before, after);
        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

        var result = new CommandResult
        {
            Command = "dropdown",
            Success = true,
            Diff = diff,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { button = buttonName, item = itemName }
                : new { button = buttonName, item = itemName, tab = tabName },
            DurationMs = elapsed
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }
}

public class ContextTabsCommand : ICommand
{
    public string Name => "context-tabs";
    public string Description => "List currently visible contextual ribbon tabs";
    public string Usage => "context-tabs";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        var rootChildren = SafeGetChildren(window, 40000);
        var mMainTabs = FindChildByAutoId(rootChildren, "mMainTabs");
        if (mMainTabs == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "mMainTabs", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var contextualTabs = new List<object>();
        foreach (var c in SafeGetChildren(mMainTabs, 5000))
        {
            try
            {
                if (c.ControlType != ControlType.Button) continue;
                var name = c.Name ?? "";
                if (string.IsNullOrEmpty(name)) continue;
                if (name.Contains("Modify", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains(" | ", StringComparison.Ordinal) ||
                    c.BoundingRectangle.Width > 10 && name.Contains("Context", StringComparison.OrdinalIgnoreCase))
                {
                    contextualTabs.Add(new
                    {
                        name,
                        automationId = Truncate(SafeGetAutoId(c), 30),
                        enabled = c.IsEnabled,
                        visible = c.IsOffscreen == false
                    });
                }
            }
            catch { }
        }

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
        var result = new CommandResult
        {
            Command = "context-tabs",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { count = contextualTabs.Count }
                : new { tabs = contextualTabs, count = contextualTabs.Count },
            DurationMs = elapsed
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }

    private static AutomationElement? FindChildByAutoId(AutomationElement[] children, string autoId)
    {
        foreach (var c in children)
        {
            try { if (SafeGetAutoId(c) == autoId) return c; } catch { }
        }
        return null;
    }
}

public class QatCommand : ICommand
{
    public string Name => "qat";
    public string Description => "List or click Quick Access Toolbar buttons";
    public string Usage => "qat [click <button-name>]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        var qat = FindQat(window);
        if (qat == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "Quick Access Toolbar (mQAT)", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var buttons = new List<object>();
        foreach (var c in SafeGetChildren(qat, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.Button)
                {
                    var name = c.Name ?? "";
                    if (!string.IsNullOrEmpty(name) && !name.StartsWith("UIFramework") && !name.StartsWith("Autodesk"))
                    {
                        buttons.Add(new
                        {
                            name,
                            automationId = Truncate(SafeGetAutoId(c), 25),
                            enabled = c.IsEnabled,
                            visible = c.IsOffscreen == false
                        });
                    }
                }
            }
            catch { }
        }

        if (args.Length >= 2 && args[0].Equals("click", StringComparison.OrdinalIgnoreCase))
        {
            var targetName = string.Join(" ", args.Skip(1));
            var targetButton = buttons.FirstOrDefault(b =>
            {
                var dict = (IDictionary<string, object>)b;
                return dict.TryGetValue("name", out var n) && n?.ToString()?.Contains(targetName, StringComparison.OrdinalIgnoreCase) == true;
            });

            if (targetButton == null)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", $"QAT button '{targetName}'", buttons.Select(b => { var d = (IDictionary<string, object>)b; return d["name"]?.ToString() ?? ""; }).ToList(), CoreSettings.GlobalOptions));
                return 1;
            }

            var dict2 = (IDictionary<string, object>)targetButton;
            var nameStr = dict2["name"]?.ToString() ?? "";

            var before = OutputFormatter.CaptureState(window);

            AutomationElement? targetEl = null;
            foreach (var c in SafeGetChildren(qat, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Button && (c.Name ?? "").Contains(targetName, StringComparison.OrdinalIgnoreCase))
                    { targetEl = c; break; }
                }
                catch { }
            }

            if (targetEl == null || !TryClick(targetEl, nameStr))
            {
                Console.Write(OutputFormatter.FormatError("ClickFailed", nameStr, null, CoreSettings.GlobalOptions));
                return 1;
            }

            var after = OutputFormatter.CaptureState(window);
            var diff = OutputFormatter.ComputeDiff(before, after);
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

            var result = new CommandResult
            {
                Command = "qat",
                Success = true,
                Diff = diff,
                Data = CoreSettings.Verbosity == "minimal"
                    ? new { action = "clicked", button = nameStr }
                    : new { action = "clicked", button = nameStr, buttons = buttons, buttonCount = buttons.Count },
                DurationMs = elapsed
            };
            if (CoreSettings.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(window);
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return 0;
        }

        var elapsed2 = (DateTime.UtcNow - start).TotalMilliseconds;
        var result2 = new CommandResult
        {
            Command = "qat",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { count = buttons.Count }
                : new { buttons, count = buttons.Count },
            DurationMs = elapsed2
        };
        Console.Write(OutputFormatter.FormatResult(result2, CoreSettings.GlobalOptions));
        return 0;
    }

    private static AutomationElement? FindQat(AutomationElement window)
    {
        var rootChildren = SafeGetChildren(window, 10000);

        foreach (var c in rootChildren)
        {
            try
            {
                var autoId = SafeGetAutoId(c);
                if (autoId.Equals("mQAT", StringComparison.OrdinalIgnoreCase) ||
                    autoId.Equals("RibbonQAT", StringComparison.OrdinalIgnoreCase) ||
                    autoId.Equals("QAT", StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            catch { }
        }

        foreach (var c in rootChildren)
        {
            try
            {
                if (c.ControlType == ControlType.ToolBar)
                {
                    foreach (var cc in SafeGetChildren(c, 3000))
                    {
                        try
                        {
                            var ccId = SafeGetAutoId(cc);
                            if (ccId.Equals("mQAT", StringComparison.OrdinalIgnoreCase) ||
                                ccId.Equals("RibbonQAT", StringComparison.OrdinalIgnoreCase))
                                return cc;
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        return null;
    }
}

public class RibbonPanelCommand : ICommand
{
    public string Name => "ribbon-panel";
    public string Description => "Show buttons in a specific ribbon panel";
    public string Usage => "ribbon-panel <tab-name> [panel-name]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "ribbon-panel <tab-name> [panel-name]", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var tabName = args[0];

        var rootChildren = SafeGetChildren(window, 40000);
        var mMainTabs = FindChildByAutoId(rootChildren, "mMainTabs");
        if (mMainTabs == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", "mMainTabs", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var tab = FindTabButton(mMainTabs, tabName);
        if (tab == null)
        {
            var suggestions = GetTabButtonNames(mMainTabs);
            Console.Write(OutputFormatter.FormatError("NotFound", $"tab '{tabName}'", suggestions, CoreSettings.GlobalOptions));
            return 1;
        }

        TryClick(tab, tabName);
        await Task.Delay(1000, ct);

        var freshRoot = SafeGetChildren(window, 20000);
        var freshMMainTabs = FindChildByAutoId(freshRoot, "mMainTabs");
        var panels = freshMMainTabs != null ? ReadPanelButtons(freshMMainTabs) : [];

        if (args.Length >= 2)
        {
            var panelFilter = args[1];
            panels = panels.Where(p => p.panelName.Contains(panelFilter, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
        var result = new CommandResult
        {
            Command = "ribbon-panel",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { tab = tabName, panelCount = panels.Count }
                : new { tab = tabName, panels = panels.Select(p => new { panel = p.panelName, buttons = p.buttons.Select(b => new { name = b.btnName, automationId = b.btnId }).ToList() }).ToList(), panelCount = panels.Count },
            DurationMs = elapsed
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }

    private static AutomationElement? FindChildByAutoId(AutomationElement[] children, string autoId)
    {
        foreach (var c in children)
        {
            try { if (SafeGetAutoId(c) == autoId) return c; } catch { }
        }
        return null;
    }

    private static AutomationElement? FindTabButton(AutomationElement mMainTabs, string name)
    {
        foreach (var c in SafeGetChildren(mMainTabs, 5000))
        {
            try
            {
                if (c.ControlType != ControlType.Button) continue;
                var cName = c.Name ?? "";
                var autoId = SafeGetAutoId(c);
                if (cName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    autoId.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                    cName.Contains(name, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            catch { }
        }
        return null;
    }

    private static List<string> GetTabButtonNames(AutomationElement mMainTabs)
    {
        var names = new List<string>();
        foreach (var c in SafeGetChildren(mMainTabs, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.Button)
                {
                    var n = c.Name ?? "";
                    if (!string.IsNullOrEmpty(n))
                        names.Add(n);
                }
            }
            catch { }
        }
        return names;
    }

    private static List<(string panelName, List<(string btnName, string btnId)> buttons)> ReadPanelButtons(AutomationElement mMainTabs)
    {
        var result = new List<(string, List<(string, string)>)>();

        AutomationElement? activeTabButton = null;
        foreach (var c in SafeGetChildren(mMainTabs, 3000))
        {
            try
            {
                if (c.ControlType == ControlType.Button)
                {
                    var rect = c.BoundingRectangle;
                    if (rect.Width > 10 && c.IsOffscreen == false)
                    {
                        activeTabButton = c;
                        break;
                    }
                }
            }
            catch { }
        }
        if (activeTabButton == null) return result;

        AutomationElement? panelsList = null;
        try
        {
            foreach (var c1 in SafeGetChildren(activeTabButton, 3000))
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
