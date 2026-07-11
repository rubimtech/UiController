using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class PatternsCommand : ICommand
{
    public string Name => "patterns";
    public string Description => "Show all available UIA patterns for an element (ValuePattern, TogglePattern, ExpandCollapse, SelectionItem, Grid, Table, Scroll, RangeValue, Window, Text, etc.)";
    public string Usage => "patterns <name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var name = string.Join(" ", args);
        if (string.IsNullOrEmpty(name))
        {
            LoggingService.Error("PatternsCommand", "Usage: patterns <name>");
            return Task.FromResult(1);
        }

        var element = AutomationHelper.FindFirstEnabledVisible(window, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var patterns = new List<Dictionary<string, object?>>();

        TryAddInvoke(patterns, element);
        TryAddValue(patterns, element);
        TryAddToggle(patterns, element);
        TryAddSelectionItem(patterns, element);
        TryAddExpandCollapse(patterns, element);
        TryAddGrid(patterns, element);
        TryAddGridItem(patterns, element);
        TryAddRangeValue(patterns, element);
        TryAddScroll(patterns, element);
        TryAddSelection(patterns, element);
        TryAddWindow(patterns, element);
        TryAddTransform(patterns, element);
        TryAddDock(patterns, element);
        TryAddText(patterns, element);

        var result = new CommandResult
        {
            Command = "patterns",
            Success = true,
            Data = new
            {
                element = new
                {
                    name = SafeGet(() => element.Name),
                    controlType = SafeGet(() => element.ControlType.ToString()),
                    automationId = SafeGet(() => element.AutomationId),
                    enabled = SafeGet(() => element.IsEnabled),
                    visible = SafeGet(() => element.IsOffscreen == false)
                },
                patternCount = patterns.Count,
                patterns
            }
        };
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }

    private static void TryAddInvoke(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            if (el.Patterns.Invoke.Pattern != null)
                list.Add(new() { ["name"] = "Invoke", ["available"] = true, ["canInvoke"] = true });
        }
        catch { }
    }

    private static void TryAddValue(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Value.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "Value", ["available"] = true, ["value"] = p.Value, ["isReadOnly"] = p.IsReadOnly });
        }
        catch { }
    }

    private static void TryAddToggle(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Toggle.Pattern;
            if (p != null)
            {
                var state = p.ToggleState;
                list.Add(new() { ["name"] = "Toggle", ["available"] = true, ["toggleState"] = state.ToString() });
            }
        }
        catch { }
    }

    private static void TryAddSelectionItem(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.SelectionItem.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "SelectionItem", ["available"] = true, ["isSelected"] = p.IsSelected });
        }
        catch { }
    }

    private static void TryAddExpandCollapse(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.ExpandCollapse.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "ExpandCollapse", ["available"] = true, ["expandCollapseState"] = p.ExpandCollapseState.ToString() });
        }
        catch { }
    }

    private static void TryAddGrid(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Grid.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "Grid", ["available"] = true, ["rowCount"] = p.RowCount, ["columnCount"] = p.ColumnCount });
        }
        catch { }
    }

    private static void TryAddGridItem(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.GridItem.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "GridItem", ["available"] = true, ["row"] = p.Row, ["column"] = p.Column, ["rowSpan"] = p.RowSpan, ["columnSpan"] = p.ColumnSpan });
        }
        catch { }
    }

    private static void TryAddRangeValue(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.RangeValue.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "RangeValue", ["available"] = true, ["value"] = p.Value, ["minimum"] = p.Minimum, ["maximum"] = p.Maximum, ["smallChange"] = p.SmallChange, ["largeChange"] = p.LargeChange });
        }
        catch { }
    }

    private static void TryAddScroll(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Scroll.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "Scroll", ["available"] = true, ["horizontallyScrollable"] = p.HorizontallyScrollable, ["verticallyScrollable"] = p.VerticallyScrollable, ["horizontalScrollPercent"] = p.HorizontalScrollPercent, ["verticalScrollPercent"] = p.VerticalScrollPercent, ["horizontalViewSize"] = p.HorizontalViewSize, ["verticalViewSize"] = p.VerticalViewSize });
        }
        catch { }
    }

    private static void TryAddSelection(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Selection.Pattern;
            if (p != null)
            {
                var selection = p.Selection.ValueOrDefault;
                list.Add(new() { ["name"] = "Selection", ["available"] = true, ["canSelectMultiple"] = p.CanSelectMultiple, ["isSelectionRequired"] = p.IsSelectionRequired, ["selectionCount"] = selection?.Length ?? 0 });
            }
        }
        catch { }
    }

    private static void TryAddWindow(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Window.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "Window", ["available"] = true, ["canMaximize"] = p.CanMaximize, ["canMinimize"] = p.CanMinimize, ["windowVisualState"] = p.WindowVisualState.ToString(), ["windowInteractionState"] = p.WindowInteractionState.ToString() });
        }
        catch { }
    }

    private static void TryAddTransform(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Transform.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "Transform", ["available"] = true, ["canMove"] = p.CanMove, ["canResize"] = p.CanResize, ["canRotate"] = p.CanRotate });
        }
        catch { }
    }

    private static void TryAddDock(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Dock.Pattern;
            if (p != null)
                list.Add(new() { ["name"] = "Dock", ["available"] = true, ["dockPosition"] = p.DockPosition.ToString() });
        }
        catch { }
    }

    private static void TryAddText(List<Dictionary<string, object?>> list, AutomationElement el)
    {
        try
        {
            var p = el.Patterns.Text.Pattern;
            if (p != null)
            {
                var text = p.DocumentRange?.GetText(5000);
                list.Add(new() { ["name"] = "Text", ["available"] = true, ["textPreview"] = text?.Length > 200 ? text[..200] + "..." : text });
            }
        }
        catch { }
    }

    private static string SafeGet(Func<object?> f) { try { return f()?.ToString() ?? ""; } catch { return ""; } }
}
