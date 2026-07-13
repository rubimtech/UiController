using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class AiFindCommand : ICommand
{
    public string Name => "ai-find";
    public string Description => "Multi-strategy element search — tries name, automation ID, locale, regex, parent context, siblings";
    public string Usage => "ai-find <query> [--parent <p>] [--type <t>] [--tab <t>] [--deep] [--max N]";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var start = DateTime.UtcNow;

        if (args.Length == 0 || args[0].StartsWith("--"))
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "ai-find <query> [options]", null, CoreSettings.GlobalOptions));
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

        var visionCached = LlmVisionCache.Get(query);
        if (visionCached != null && !string.IsNullOrEmpty(visionCached.AutomationId))
        {
            var cachedEl = FindFirstEnabledVisible(window, visionCached.AutomationId);
            if (cachedEl != null)
            {
                ElementSearchStrategies.AddUnique(raw, cachedEl, "visionCache");
                strategiesUsed.Add("visionCache");
                goto Output;
            }
        }

        if (tabName != null)
        {
            var tab = FindFirstEnabledVisible(window, tabName);
            if (tab != null)
            {
                TryClick(tab, tabName);
                await Task.Delay(300, ct);
                strategiesUsed.Add("tab-switch");
            }
        }

        var searchRoot = window;
        if (parentName != null)
        {
            var parent = FindFirstEnabledVisible(window, parentName);
            if (parent != null)
            {
                searchRoot = parent;
                strategiesUsed.Add("parent-scoped");
            }
        }

        if (ElementSearchStrategies.AiFindNameStrategy(searchRoot, query, raw, maxCandidates))
            strategiesUsed.Add("name");
        if (raw.Count > 0 && !deep)
            goto Output;

        if (ElementSearchStrategies.AiFindLocaleStrategy(searchRoot, query, raw, maxCandidates))
            strategiesUsed.Add("locale");
        if (raw.Count > 0 && !deep)
            goto Output;

        if (ElementSearchStrategies.AiFindAutoIdStrategy(searchRoot, query, raw, maxCandidates))
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
            .GroupBy(c => ElementSearchStrategies.ElementKey(c.element))
            .Select(g => g.First())
            .Where(c => filterType == null || c.element.ControlType.ToString().Equals(filterType, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => ElementSearchStrategies.MatchTypeRank(c.matchType))
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
            }, CoreSettings.GlobalOptions));
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
        }, CoreSettings.GlobalOptions));
        return 0;
    }

    private static bool RunRegexStrategy(AutomationElement root, string query, List<(AutomationElement, string)> results, int max)
    {
        try
        {
            var before = results.Count;
            var regex = new Regex(query, RegexOptions.IgnoreCase);
            ElementSearchStrategies.BfsScan(root, el =>
            {
                if (results.Count >= max) return;
                var name = SafeGetName(el);
                if (!string.IsNullOrEmpty(name) && regex.IsMatch(name))
                    ElementSearchStrategies.AddUnique(results, el, "regex");
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
                        ElementSearchStrategies.AddUnique(results, sibling, "sibling");
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
}
