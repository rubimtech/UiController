using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class DumpPatternsCommand : ICommand
{
    public string Name => "dump-patterns";
    public string Description => "Dump UIA tree with supported patterns for each element. Usage: dump-patterns [depth] [--type <ct>] [--filter-name <name>]";
    public string Usage => "dump-patterns [depth] [--type <ct>] [--filter-name <name>]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var depth = 2;
        string? filterType = null;
        string? filterName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--type" && i + 1 < args.Length) filterType = args[++i];
            else if (args[i] == "--filter-name" && i + 1 < args.Length) filterName = args[++i];
            else if (int.TryParse(args[i], out var d)) depth = d;
        }

        var tree = new List<object>();
        DumpWithPatterns(revitWindow, tree, 0, depth, filterType, filterName);

        var result = new CommandResult
        {
            Command = "dump-patterns",
            Success = true,
            Data = new
            {
                depth,
                filterType,
                filterName,
                totalElements = tree.Count,
                tree
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }

    private void DumpWithPatterns(AutomationElement parent, List<object> output, int currentDepth, int maxDepth, string? filterType, string? filterName)
    {
        if (currentDepth > maxDepth) return;

        foreach (var child in AutomationHelper.SafeGetChildren(parent, 5000))
        {
            try
            {
                var name = child.Name ?? "";
                var ctrlType = child.ControlType.ToString();

                if (filterType != null && !ctrlType.Contains(filterType, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (filterName != null && !name.Contains(filterName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var patterns = DetectPatterns(child);

                var entry = new Dictionary<string, object?>
                {
                    ["name"] = Truncate(name, 80),
                    ["controlType"] = ctrlType,
                    ["depth"] = currentDepth,
                    ["automationId"] = SafeGet(() => child.AutomationId),
                    ["enabled"] = SafeGet(() => child.IsEnabled),
                    ["visible"] = SafeGet(() => child.IsOffscreen == false),
                    ["patterns"] = patterns.Count > 0 ? patterns : null
                };

                output.Add(entry);

                if (currentDepth < maxDepth)
                {
                    var subItems = new List<object>();
                    DumpWithPatterns(child, subItems, currentDepth + 1, maxDepth, filterType, filterName);
                    if (subItems.Count > 0)
                        entry["children"] = subItems;
                }
            }
            catch { }
        }
    }

    private static List<string> DetectPatterns(AutomationElement el)
    {
        var result = new List<string>();
        try { if (el.Patterns.Invoke.Pattern != null) result.Add("Invoke"); } catch { }
        try { if (el.Patterns.Value.Pattern != null) result.Add("Value"); } catch { }
        try { if (el.Patterns.Toggle.Pattern != null) result.Add("Toggle"); } catch { }
        try { if (el.Patterns.SelectionItem.Pattern != null) result.Add("SelectionItem"); } catch { }
        try { if (el.Patterns.ExpandCollapse.Pattern != null) result.Add("ExpandCollapse"); } catch { }
        try { if (el.Patterns.Grid.Pattern != null) result.Add("Grid"); } catch { }
        try { if (el.Patterns.GridItem.Pattern != null) result.Add("GridItem"); } catch { }
        try { if (el.Patterns.RangeValue.Pattern != null) result.Add("RangeValue"); } catch { }
        try { if (el.Patterns.Scroll.Pattern != null) result.Add("Scroll"); } catch { }
        try { if (el.Patterns.Selection.Pattern != null) result.Add("Selection"); } catch { }
        try { if (el.Patterns.Window.Pattern != null) result.Add("Window"); } catch { }
        try { if (el.Patterns.Text.Pattern != null) result.Add("Text"); } catch { }
        try { if (el.Patterns.Dock.Pattern != null) result.Add("Dock"); } catch { }
        try { if (el.Patterns.Transform.Pattern != null) result.Add("Transform"); } catch { }
        return result;
    }

    private static string SafeGet(Func<object?> f) { try { return f()?.ToString() ?? ""; } catch { return ""; } }
    private static string Truncate(string s, int max) { return s.Length <= max ? s : s[..(max - 3)] + "..."; }
}
