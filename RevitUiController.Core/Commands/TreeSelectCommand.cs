using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class TreeSelectCommand : ICommand
{
    public string Name => "tree-select";
    public string Description => "Select item in TreeView by path. Usage: tree-select <path>";
    public string Usage => "tree-select <path>";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "tree-select <path>", null, CoreSettings.GlobalOptions));
            return 1;
        }

        var path = string.Join(" ", args);
        var segments = path.Split(['/', '\\', '>'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        AutomationElement? tree = FindFirstChildByType(window, ControlType.Tree);
        if (tree == null)
        {
            Console.Write(OutputFormatter.FormatError("TreeNotFound", path, ["No Tree control found"], CoreSettings.GlobalOptions));
            return 1;
        }

        AutomationElement? current = tree;
        for (int i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            var treeItem = FindTreeItem(current, segment);
            if (treeItem == null)
            {
                Console.Write(OutputFormatter.FormatError("TreeItemNotFound", segment, null, CoreSettings.GlobalOptions));
                return 1;
            }

            if (i < segments.Length - 1)
            {
                var expand = treeItem.Patterns.ExpandCollapse.Pattern;
                if (expand != null && expand.ExpandCollapseState == ExpandCollapseState.Collapsed)
                {
                    expand.Expand();
                    await Task.Delay(300, ct);
                }
            }
            else
            {
                try
                {
                    treeItem.Click();
                    await Task.Delay(200, ct);
                }
                catch
                {
                    Console.Write(OutputFormatter.FormatError("SelectFailed", segment, null, CoreSettings.GlobalOptions));
                    return 1;
                }
            }

            current = treeItem;
        }

        var result = new CommandResult
        {
            Command = "tree-select",
            Success = true,
            Data = new { path, segments = segments.Length }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }

    private static AutomationElement? FindTreeItem(AutomationElement parent, string name)
    {
        foreach (var c in SafeGetChildren(parent, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.TreeItem &&
                    (c.Name ?? "").Contains(name, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            catch { }
        }
        return null;
    }
}
