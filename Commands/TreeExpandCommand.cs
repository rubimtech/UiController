using FlaUI.Core.AutomationElements;
using RevitUiController.Models;

namespace RevitUiController.Commands;

public class TreeExpandCommand : ICommand
{
    public string Name => "tree-expand";
    public string Description => "Expand a TreeView node and dump its subtree. Usage: tree-expand <name> [--all]";
    public string Usage => "tree-expand <name> [--all] [--depth N]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: tree-expand <name> [--all] [--depth N]");
            return Task.FromResult(1);
        }

        var expandAll = args.Contains("--all");
        var depthArg = args.Select((v, i) => v == "--depth" && i + 1 < args.Length ? args[i + 1] : null).FirstOrDefault(v => v != null);
        int maxDepth = depthArg != null && int.TryParse(depthArg, out var d) ? d : (expandAll ? 20 : 5);

        var name = args.First(a => !a.StartsWith("--"));
        var element = AutomationHelper.FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.IsPretty));
            return Task.FromResult(1);
        }

        var items = new List<object>();
        ExpandSubtree(element, items, 0, maxDepth, expandAll);

        var result = new CommandResult
        {
            Command = "tree-expand",
            Success = true,
            Data = new
            {
                element = new { name = SafeGet(() => element.Name), controlType = SafeGet(() => element.ControlType.ToString()) },
                totalItems = items.Count,
                items
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, Program.IsPretty));
        return Task.FromResult(0);
    }

    private void ExpandSubtree(AutomationElement parent, List<object> output, int depth, int maxDepth, bool expandAll)
    {
        if (depth > maxDepth) return;

        foreach (var child in AutomationHelper.SafeGetChildren(parent, 5000))
        {
            try
            {
                var name = child.Name ?? "";
                var ctrlType = child.ControlType.ToString();
                var expanded = TryGetExpandState(child);

                var entry = new Dictionary<string, object?>
                {
                    ["name"] = Truncate(name, 80),
                    ["controlType"] = ctrlType,
                    ["depth"] = depth,
                    ["expandCollapseState"] = expanded ?? "None",
                    ["automationId"] = SafeGet(() => child.AutomationId),
                    ["enabled"] = SafeGet(() => child.IsEnabled),
                    ["visible"] = SafeGet(() => child.IsOffscreen == false)
                };

                if ((expanded == "Expanded" || expanded == "PartiallyExpanded") && depth < maxDepth)
                {
                    if (expandAll && parent == child)
                    {
                    }
                    else if (expanded == "PartiallyExpanded")
                    {
                        try { child.Patterns.ExpandCollapse.Pattern?.Expand(); } catch { }
                    }
                }

                output.Add(entry);

                if (depth < maxDepth)
                {
                    var subItems = new List<object>();
                    ExpandSubtree(child, subItems, depth + 1, maxDepth, expandAll);
                    if (subItems.Count > 0)
                        entry["children"] = subItems;
                }
            }
            catch { }
        }
    }

    private static string? TryGetExpandState(AutomationElement element)
    {
        try
        {
            return element.Patterns.ExpandCollapse.Pattern?.ExpandCollapseState.ToString();
        }
        catch { return null; }
    }

    private static string SafeGet(Func<object?> f) { try { return f()?.ToString() ?? ""; } catch { return ""; } }
    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 3)] + "...";
}
