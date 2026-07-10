using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

namespace RevitUiController.Commands;

public class AiFindCommand : ICommand
{
    public string Name => "ai-find";
    public string Description => "Multi-strategy element search — tries name, automation ID, locale, regex, parent context, siblings";
    public string Usage => "ai-find <query> [--parent <p>] [--type <t>] [--tab <t>] [--deep] [--max N]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length == 0 || args[0].StartsWith("--"))
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "ai-find <query> [options]", null, Program.IsPretty));
            return 1;
        }

        var query = args[0];
        string? parentName = null;
        string? filterType = null;
        string? tabName = null;
        var deep = false;
        var maxCandidates = 20;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--parent" when i + 1 < args.Length: parentName = args[++i]; break;
                case "--type" when i + 1 < args.Length: filterType = args[++i]; break;
                case "--tab" when i + 1 < args.Length: tabName = args[++i]; break;
                case "--deep": deep = true; break;
                case "--max" when i + 1 < args.Length && int.TryParse(args[++i], out var m): maxCandidates = Math.Clamp(m, 1, 100); break;
            }
        }

        var raw = new List<(AutomationElement element, string matchType)>();
        var strategiesUsed = new List<string>();

        if (tabName != null)
        {
            var tab = FindFirstEnabledVisible(revitWindow, tabName);
            if (tab != null)
            {
                TryClick(tab, tabName);
                await Task.Delay(300, ct);
                strategiesUsed.Add("tab-switch");
            }
        }

        var searchRoot = revitWindow;
        if (parentName != null)
        {
            var parent = FindFirstEnabledVisible(revitWindow, parentName);
            if (parent != null)
            {
                searchRoot = parent;
                strategiesUsed.Add("parent-scoped");
            }
        }

        if (RunNameStrategy(searchRoot, query, raw, maxCandidates))
            strategiesUsed.Add("name");
        if (raw.Count > 0 && !deep)
            goto Output;

        if (RunLocaleStrategy(searchRoot, query, raw, maxCandidates))
            strategiesUsed.Add("locale");
        if (raw.Count > 0 && !deep)
            goto Output;

        if (RunAutoIdStrategy(searchRoot, query, raw, maxCandidates))
            strategiesUsed.Add("automationId");
        if (raw.Count > 0 && !deep)
            goto Output;

        if (RunRegexStrategy(searchRoot, query, raw, maxCandidates))
            strategiesUsed.Add("regex");
        if (raw.Count > 0 && !deep)
            goto Output;

        if (deep)
        {
            if (RunSiblingStrategy(searchRoot, raw, maxCandidates))
                strategiesUsed.Add("sibling");
        }

    Output:
        var ranked = raw
            .GroupBy(c => ElementKey(c.element))
            .Select(g => g.First())
            .Where(c => filterType == null || c.element.ControlType.ToString().Equals(filterType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => MatchTypeRank(c.matchType))
            .ThenBy(c => SafeGetName(c.element).Length)
            .Take(maxCandidates)
            .ToList();

        var candidates = ranked.Select(c => new
        {
            name = SafeGetName(c.element),
            automationId = SafeGetAutoId(c.element),
            controlType = c.element.ControlType.ToString(),
            enabled = SafeIsEnabled(c.element),
            matchType = c.matchType,
            boundingRect = SafeGetRect(c.element)
        }).ToList();

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

        if (candidates.Count == 0)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "ai-find",
                Success = false,
                Error = $"No candidates found for '{query}'",
                Data = new
                {
                    query,
                    candidates = Array.Empty<object>(),
                    strategiesUsed = strategiesUsed.Distinct().ToList(),
                    suggestions = new[]
                    {
                        "Try different spelling",
                        "Use --type Button|Tab|Edit",
                        "Use --parent to narrow scope",
                        "Use --tab for ribbon context"
                    }
                },
                DurationMs = elapsed
            }, Program.IsPretty));
            return 1;
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "ai-find",
            Success = true,
            Data = new
            {
                query,
                candidates,
                totalCandidates = candidates.Count,
                strategiesUsed = strategiesUsed.Distinct().ToList(),
                bestMatch = candidates[0].name
            },
            DurationMs = elapsed
        }, Program.IsPretty));
        return 0;
    }

    private static bool RunNameStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
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

    private static bool RunLocaleStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
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

    private static bool RunAutoIdStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
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

    private static bool RunRegexStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
    {
        try
        {
            var before = results.Count;
            var regex = new Regex(query, RegexOptions.IgnoreCase);
            BfsScan(root, el =>
            {
                if (results.Count >= max) return;
                var name = SafeGetName(el);
                if (!string.IsNullOrEmpty(name) && regex.IsMatch(name))
                    AddUnique(results, el, "regex");
            });
            return results.Count > before;
        }
        catch { return false; }
    }

    private static bool RunSiblingStrategy(AutomationElement root, List<(AutomationElement, string)> results, int max)
    {
        if (results.Count == 0) return false;
        var before = results.Count;

        foreach (var (el, mt) in results.ToList())
        {
            if (results.Count >= max * 2) break;
            try
            {
                var parent = FindParent(el);
                if (parent == null) continue;

                var anchorRect = el.BoundingRectangle;

                foreach (var sibling in SafeGetChildren(parent, 3000))
                {
                    if (results.Count >= max * 2) break;
                    if (sibling.Equals(el)) continue;

                    var sr = sibling.BoundingRectangle;

                    if (Math.Abs(sr.Y - anchorRect.Y) < 30 && Math.Abs(sr.Height - anchorRect.Height) < 15)
                        AddUnique(results, sibling, "sibling");
                }
            }
            catch { }
        }
        return results.Count > before;
    }

    private static AutomationElement? FindParent(AutomationElement child)
    {
        try { return child.Parent; } catch { return null; }
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
        "regex" => 3,
        "locale" => 4,
        "automationId" => 5,
        "sibling" => 6,
        _ => 10
    };

    private static bool SafeIsEnabled(AutomationElement el)
    {
        try { return el.IsEnabled; } catch { return false; }
    }

    private static RectInfo? SafeGetRect(AutomationElement el)
    {
        try
        {
            var r = el.BoundingRectangle;
            return new RectInfo(r.X, r.Y, r.Width, r.Height);
        }
        catch { return null; }
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
}
