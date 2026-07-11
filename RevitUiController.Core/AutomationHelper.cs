using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;

namespace UiController.Core;

public static class AutomationHelper
{
    public static AutomationElement[] SafeGetChildren(AutomationElement element, int timeoutMs = 4000)
    {
        try
        {
            var task = Task.Run(() => element.FindAllChildren().ToArray());
            if (task.Wait(timeoutMs))
                return task.Result;
            LoggingService.Warn(nameof(SafeGetChildren), $"Timeout after {timeoutMs}ms");
            return Array.Empty<AutomationElement>();
        }
        catch (Exception ex)
        {
            LoggingService.Warn(nameof(SafeGetChildren), ex.Message);
            return Array.Empty<AutomationElement>();
        }
    }

    public static List<AutomationElement> FindControlsByName(AutomationElement parent, string name, int maxResults = 100)
    {
        var results = new List<AutomationElement>();

        var rootChildren = SafeGetChildren(parent, 8000);
        if (rootChildren.Length == 0) return results;

        var queue = new Queue<AutomationElement>();
        foreach (var child in rootChildren)
            queue.Enqueue(child);

        var scanned = 0;
        while (queue.Count > 0 && scanned < 500 && results.Count < maxResults)
        {
            var current = queue.Dequeue();
            scanned++;

            var children = SafeGetChildren(current, 3000);
            foreach (var c in children)
            {
                try
                {
                    var cName = c.Name ?? "";
                    var autoId = c.AutomationId ?? "";
                    if (cName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                        autoId.Contains(name, StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(c);
                        if (results.Count >= maxResults) return results;
                    }
                    queue.Enqueue(c);
                }
                catch { }
            }
        }
        return results;
    }

    public static List<AutomationElement> FindWindowsWithName(AutomationElement parent, string? nameFilter)
    {
        var results = new List<AutomationElement>();

        var directChildren = SafeGetChildren(parent);
        foreach (var w in directChildren)
        {
            if (w.ControlType == ControlType.Window && !string.IsNullOrEmpty(w.Name))
            {
                if (nameFilter == null || w.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                    results.Add(w);
            }
        }
        foreach (var c in directChildren)
        {
            var subChildren = SafeGetChildren(c);
            foreach (var w in subChildren)
            {
                try
                {
                    if (w.ControlType == ControlType.Window && !string.IsNullOrEmpty(w.Name))
                    {
                        if (nameFilter == null || w.Name.Contains(nameFilter, StringComparison.OrdinalIgnoreCase))
                            results.Add(w);
                    }
                }
                catch { }
            }
        }
        return results;
    }

    public static AutomationElement? FindFirstEnabledVisible(AutomationElement parent, string name, int maxDepth = 8)
    {
        var candidates = UiMap.IsLoaded ? UiMap.Resolve(name) : new List<SelectorCandidate>();
        var autoIds = candidates
            .Where(c => !string.IsNullOrEmpty(c.AutomationId))
            .Select(c => c.AutomationId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateNames = candidates
            .Where(c => !string.IsNullOrEmpty(c.Name))
            .Select(c => c.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rootChildren = SafeGetChildren(parent, 8000);
        if (rootChildren.Length == 0) return null;

        foreach (var child in rootChildren)
        {
            var queue = new Queue<(AutomationElement element, int depth)>();
            queue.Enqueue((child, 1));
            var scanned = 0;

            while (queue.Count > 0 && scanned < 2000)
            {
                var (current, depth) = queue.Dequeue();
                scanned++;
                if (depth > maxDepth) continue;

                var children = SafeGetChildren(current, 3000);
                foreach (var c in children)
                {
                    try
                    {
                        if (!c.IsEnabled || c.IsOffscreen != false)
                        {
                            queue.Enqueue((c, depth + 1));
                            continue;
                        }

                        var cName = c.Name ?? "";
                        var autoId = c.AutomationId ?? "";

                        if (autoIds.Count > 0 && autoIds.Contains(autoId))
                            return c;

                        if (candidateNames.Count > 0 && candidateNames.Contains(cName))
                            return c;

                        if (cName.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                            autoId.Equals(name, StringComparison.OrdinalIgnoreCase) ||
                            cName.StartsWith(name, StringComparison.OrdinalIgnoreCase) ||
                            cName.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                            autoId.Contains(name, StringComparison.OrdinalIgnoreCase))
                        {
                            return c;
                        }

                        queue.Enqueue((c, depth + 1));
                    }
                    catch { }
                }
            }
        }

        if (candidates.Count > 0)
        {
            foreach (var candidate in candidates)
            {
                if (candidate.Fallbacks != null)
                {
                    foreach (var fb in candidate.Fallbacks)
                    {
                        var result = FindFirstEnabledVisible(parent, fb, maxDepth);
                        if (result != null) return result;
                    }
                }
            }
        }

        return null;
    }

    public static List<AutomationElement> FindActiveDialogs(AutomationElement parent)
    {
        var results = new List<AutomationElement>();
        foreach (var c in SafeGetChildren(parent))
        {
            try
            {
                if (c.ControlType == ControlType.Window && !string.IsNullOrEmpty(c.Name) && c.IsOffscreen == false)
                    results.Add(c);
            }
            catch { }
        }
        return results;
    }

    public static AutomationElement? FindFirstChildByType(AutomationElement parent, ControlType type, string? nameContains = null)
    {
        foreach (var c in SafeGetChildren(parent))
        {
            try
            {
                if (c.ControlType == type)
                {
                    if (nameContains == null || (c.Name ?? "").Contains(nameContains, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
            }
            catch { }
        }
        return null;
    }

    public static bool TryClick(AutomationElement element, string label)
    {
        try
        {
            element.Click();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void SendTextSafe(AutomationElement element, string text)
    {
        try
        {
            var valuePattern = element.Patterns.Value.Pattern;
            if (valuePattern != null)
            {
                valuePattern.SetValue(text);
                return;
            }
        }
        catch { }

        var escaped = EscapeSendKeys(text);
        global::System.Windows.Forms.SendKeys.SendWait(escaped);
    }

    private static string EscapeSendKeys(string text)
    {
        return Regex.Replace(text, @"([+^%~(){}\[\]])", "{$1}");
    }

    public static string SafeGetAutoId(AutomationElement e) { try { return e.AutomationId ?? ""; } catch { return ""; } }
    public static string SafeGetName(AutomationElement e) { try { return e.Name ?? ""; } catch { return ""; } }

    public static string Truncate(string s, int max)
    {
        return s.Length <= max ? s : s[..(max - 3)] + "...";
    }

    public static string[] Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuote = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuote = !inQuote;
            }
            else if (c == ' ' && !inQuote)
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            tokens.Add(current.ToString());

        return tokens.ToArray();
    }

    public static AutomationElement? FindFieldByLabel(AutomationElement dialog, string label)
    {
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Text && c.Name != null &&
                        c.Name.Contains(label, StringComparison.OrdinalIgnoreCase))
                    {
                        var sibling = FindNextSibling(c);
                        if (sibling != null && (sibling.ControlType == ControlType.Edit || sibling.ControlType == ControlType.ComboBox))
                            return sibling;
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public static AutomationElement? FindCheckboxByLabel(AutomationElement dialog, string label)
    {
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.CheckBox &&
                        (c.Name ?? "").Contains(label, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public static AutomationElement? FindComboByLabel(AutomationElement dialog, string label)
    {
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.ComboBox &&
                        (c.Name ?? "").Contains(label, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public static AutomationElement? FindDropdownItem(AutomationElement combo, string text)
    {
        try
        {
            foreach (var c in SafeGetChildren(combo, 3000))
            {
                try
                {
                    if ((c.Name ?? "").Contains(text, StringComparison.OrdinalIgnoreCase))
                        return c;
                    foreach (var cc in SafeGetChildren(c, 2000))
                    {
                        try
                        {
                            if ((cc.Name ?? "").Contains(text, StringComparison.OrdinalIgnoreCase))
                                return cc;
                        }
                        catch { }
                    }
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public static AutomationElement? FindDialogButton(AutomationElement dialog, string name)
    {
        try
        {
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Button &&
                        (c.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }

    public static bool? GetIsChecked(AutomationElement checkbox)
    {
        try
        {
            var toggle = checkbox.Patterns.Toggle.Pattern;
            if (toggle != null)
                return toggle.ToggleState == ToggleState.On;
        }
        catch { }
        return null;
    }

    public static AutomationElement? FindNextSibling(AutomationElement element)
    {
        try
        {
            var parent = FindParent(element);
            if (parent == null) return null;
            var children = SafeGetChildren(parent, 3000);
            for (int i = 0; i < children.Length - 1; i++)
            {
                if (children[i].Equals(element) || (children[i].Name == element.Name && children[i].ControlType == element.ControlType))
                    return children[i + 1];
            }
        }
        catch { }
        return null;
    }

    public static AutomationElement? FindParent(AutomationElement child)
    {
        try
        {
            var walker = child.Automation.TreeWalkerFactory.GetControlViewWalker();
            return walker.GetParent(child);
        }
        catch { }
        return null;
    }

    public static string FindLabelNear(AutomationElement dialog, AutomationElement field)
    {
        try
        {
            var fieldRect = field.BoundingRectangle;
            foreach (var c in SafeGetChildren(dialog, 5000))
            {
                try
                {
                    if (c.ControlType == ControlType.Text && !string.IsNullOrEmpty(c.Name))
                    {
                        var r = c.BoundingRectangle;
                        if (Math.Abs(r.Top - fieldRect.Top) < 15 && r.Right <= fieldRect.Left + 10)
                            return c.Name;
                    }
                }
                catch { }
            }
        }
        catch { }
        return "";
    }
}
