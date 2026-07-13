using System.Diagnostics;
using FlaUI.Core.AutomationElements;
using RevitUiController.Commands;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController;

public class ClickStrategy
{
    public string Name { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public long DurationMs { get; set; }
}

public static class ClickFallbackChain
{
    public static readonly string[] DefaultChain = { "uia-click", "ai-find", "vision", "win32-click", "winappdriver" };

    public static async Task<(bool Success, string Method, List<ClickStrategy> Attempts)> ExecuteAsync(
        AutomationElement window, string elementName, string[]? strategies = null,
        CancellationToken ct = default)
    {
        var attempts = new List<ClickStrategy>();
        var chain = strategies ?? DefaultChain;

        foreach (var strategy in chain)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var ok = strategy switch
                {
                    "uia-click" => await TryUiaClick(window, elementName, ct),
                    "ai-find" => await TryAiFindClick(window, elementName, ct),
                    "vision" => await TryVisionClick(window, elementName, ct),
                    "win32-click" => await TryWin32Click(window, elementName, ct),
                    "winappdriver" => await TryWinAppDriverClick(window, elementName, ct),
                    _ => false
                };
                sw.Stop();
                attempts.Add(new ClickStrategy { Name = strategy, Success = ok, DurationMs = sw.ElapsedMilliseconds });
                if (ok) return (true, strategy, attempts);
            }
            catch (Exception ex)
            {
                sw.Stop();
                attempts.Add(new ClickStrategy { Name = strategy, Success = false, Error = ex.Message, DurationMs = sw.ElapsedMilliseconds });
            }
        }
        return (false, chain[^1], attempts);
    }

    private static async Task<bool> TryUiaClick(AutomationElement window, string elementName, CancellationToken ct)
    {
        var visionCached = LlmVisionCache.Get(elementName);
        if (visionCached != null)
        {
            AutomationElement? cachedEl = null;
            if (!string.IsNullOrEmpty(visionCached.AutomationId))
                cachedEl = FindFirstEnabledVisible(window, visionCached.AutomationId);
            cachedEl ??= FindFirstEnabledVisible(window, elementName);
            if (cachedEl != null)
                return TryClick(cachedEl, elementName);
        }

        var found = ElementSearchStrategies.FindByAutoIdInRoot(window, elementName);
        if (found == null)
            found = FindFirstEnabledVisible(window, elementName);
        if (found == null)
            return false;
        return TryClick(found, elementName);
    }

    private static async Task<bool> TryAiFindClick(AutomationElement window, string elementName, CancellationToken ct)
    {
        var candidates = new List<(AutomationElement element, string matchType)>();

        if (ElementSearchStrategies.AiFindNameStrategy(window, elementName, candidates, 10))
            return TryClickBest(candidates, elementName);
        if (ElementSearchStrategies.AiFindLocaleStrategy(window, elementName, candidates, 10))
            return TryClickBest(candidates, elementName);
        if (ElementSearchStrategies.AiFindAutoIdStrategy(window, elementName, candidates, 10))
            return TryClickBest(candidates, elementName);

        return false;
    }

    private static bool TryClickBest(List<(AutomationElement element, string matchType)> candidates, string label)
    {
        var ranked = candidates
            .GroupBy(c => ElementSearchStrategies.ElementKey(c.element))
            .Select(g => g.First())
            .OrderBy(c => ElementSearchStrategies.MatchTypeRank(c.matchType))
            .ThenBy(c => SafeGetName(c.element).Length);

        foreach (var (el, _) in ranked)
        {
            if (TryClick(el, label))
                return true;
        }
        return false;
    }

    private static async Task<bool> TryVisionClick(AutomationElement window, string elementName, CancellationToken ct)
    {
        var rect = window.BoundingRectangle;
        var base64 = ScreenshotHelper.CaptureBase64((int)rect.X, (int)rect.Y, (int)rect.Width, (int)rect.Height);
        if (base64 == null)
            return false;

        var result = await LlmVisionClient.FindElementAsync(elementName, base64, timeoutSec: 15);
        if (result == null || !result.Found)
            return false;

        var elementX = (int)rect.X + result.X;
        var elementY = (int)rect.Y + result.Y;
        await MouseControl.ClickAt(elementX, elementY, ct);
        await Task.Delay(200, ct);

        LlmVisionCache.Add(elementName, new LlmVisionCache.CachedElement
        {
            Name = elementName,
            X = elementX,
            Y = elementY,
            LastSeen = DateTime.UtcNow,
            FoundBy = "vision"
        });

        return true;
    }

    private static async Task<bool> TryWin32Click(AutomationElement window, string elementName, CancellationToken ct)
    {
        var element = FindFirstEnabledVisible(window, elementName);
        if (element == null)
            return false;

        try
        {
            var hWnd = element.Properties.NativeWindowHandle;
            if (hWnd == null || hWnd == IntPtr.Zero)
                return TryClick(element, elementName);

            return Win32Helper.ClickButton(hWnd);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> TryWinAppDriverClick(AutomationElement window, string elementName, CancellationToken ct)
    {
        var wad = Program.WadClient;
        if (wad == null || !wad.IsConnected)
            return false;

        var elementId = wad.FindElement("name", elementName);
        if (elementId == null)
            elementId = wad.FindElement("accessibility id", elementName);
        if (elementId == null)
            return false;

        return wad.Click(elementId);
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

    private static bool AiFindNameStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
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

    private static bool AiFindLocaleStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
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

    private static bool AiFindAutoIdStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
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

    private static void BfsScan(AutomationElement root, Action<AutomationElement> action)
    {
        var queue = new Queue<AutomationElement>();
        foreach (var c in SafeGetChildren(root, 8000))
            queue.Enqueue(c);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            try { action(current); } catch { }
            foreach (var c in SafeGetChildren(current, 2000))
                queue.Enqueue(c);
        }
    }

    private static void AddUnique(List<(AutomationElement, string)> list, AutomationElement el, string matchType)
    {
        var key = ElementKey(el);
        if (!list.Any(c => ElementKey(c.Item1) == key))
            list.Add((el, matchType));
    }

    private static string ElementKey(AutomationElement el)
    {
        var name = SafeGetName(el);
        var id = SafeGetAutoId(el);
        var r = SafeGetRectString(el);
        return $"{name}|{id}|{r}";
    }

    private static string SafeGetRectString(AutomationElement el)
    {
        try
        {
            var r = el.BoundingRectangle;
            return $"{r.X:F0},{r.Y:F0},{r.Width:F0},{r.Height:F0}";
        }
        catch { return ""; }
    }

    private static string ExactOrStartsMatch(string query, string name)
    {
        if (name.Equals(query, StringComparison.OrdinalIgnoreCase)) return "exact";
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return "startsWith";
        return "contains";
    }

    private static int MatchTypeRank(string mt) => mt switch
    {
        "exact" => 0,
        "startsWith" => 1,
        "contains" => 2,
        "locale" => 4,
        "automationId" => 5,
        _ => 10
    };
}
