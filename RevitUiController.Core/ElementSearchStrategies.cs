using FlaUI.Core.AutomationElements;
using static UiController.Core.AutomationHelper;

namespace UiController.Core;

public static class ElementSearchStrategies
{
    public static void BfsScan(AutomationElement root, Action<AutomationElement> action, int timeoutMs = 8000)
    {
        var queue = new Queue<AutomationElement>();
        foreach (var c in SafeGetChildren(root, timeoutMs))
            queue.Enqueue(c);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            try { action(current); } catch { }
            foreach (var c in SafeGetChildren(current, 2000))
                queue.Enqueue(c);
        }
    }

    public static void AddUnique(List<(AutomationElement, string)> list, AutomationElement el, string matchType)
    {
        var key = ElementKey(el);
        if (!list.Any(c => ElementKey(c.Item1) == key))
            list.Add((el, matchType));
    }

    public static string ElementKey(AutomationElement el)
    {
        var name = SafeGetName(el);
        var id = SafeGetAutoId(el);
        var r = SafeGetRectString(el);
        return $"{name}|{id}|{r}";
    }

    public static string SafeGetRectString(AutomationElement el)
    {
        try
        {
            var r = el.BoundingRectangle;
            return $"{r.X:F0},{r.Y:F0},{r.Width:F0},{r.Height:F0}";
        }
        catch { return ""; }
    }

    public static int MatchTypeRank(string mt) => mt switch
    {
        "exact" => 0,
        "startsWith" => 1,
        "contains" => 2,
        "regex" => 3,
        "visionCache" => 3,
        "locale" => 4,
        "automationId" => 5,
        "sibling" => 6,
        _ => 10
    };

    private static string ExactOrStartsMatch(string query, string name)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase)) return "exact";
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return "startsWith";
        return "contains";
    }

    public static AutomationElement? FindByAutoIdInRoot(AutomationElement revitWindow, string nameOrId)
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

    public static bool AiFindNameStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
    {
        var before = results.Count;
        var exact = FindFirstEnabledVisible(root, query);
        if (exact != null)
            AddUnique(results, exact, ExactOrStartsMatch(query, SafeGetName(exact)));

        var all = FindControlsByName(root, query, max);
        foreach (var el in all)
        {
            if (results.Count >= max) break;
            var name = SafeGetName(el);
            var mt = name.Equals(query, StringComparison.OrdinalIgnoreCase) ? "exact"
                : name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? "startsWith"
                : "contains";
            AddUnique(results, el, mt);
        }
        return results.Count > before;
    }

    public static bool AiFindLocaleStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
    {
        var before = results.Count;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };

        var normalized = LocaleMap.Normalize(query);
        if (seen.Add(normalized))
        {
            var el = FindFirstEnabledVisible(root, normalized);
            if (el != null) AddUnique(results, el, "locale");
            foreach (var c in FindControlsByName(root, normalized, max))
            {
                if (results.Count >= max) break;
                AddUnique(results, c, "locale");
            }
        }

        foreach (var alt in LocaleMap.GetAlternatives(query))
        {
            if (!seen.Add(alt)) continue;
            var el = FindFirstEnabledVisible(root, alt);
            if (el != null) AddUnique(results, el, "locale");
            foreach (var c in FindControlsByName(root, alt, max))
            {
                if (results.Count >= max) break;
                AddUnique(results, c, "locale");
            }
        }
        return results.Count > before;
    }

    public static bool AiFindAutoIdStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
    {
        var before = results.Count;
        BfsScan(root, el =>
        {
            if (results.Count >= max) return;
            var id = SafeGetAutoId(el);
            if (!string.IsNullOrEmpty(id) && id.Contains(query, StringComparison.OrdinalIgnoreCase))
                AddUnique(results, el, "automationId");
        });
        return results.Count > before;
    }
}
