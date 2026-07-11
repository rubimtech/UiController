using System.IO;
using System.Text;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Core.Models;
using static RevitUiController.Core.AutomationHelper;
using static RevitUiController.Core.OutputFormatter;

namespace RevitUiController.Core.Commands;

public class ListWindowsCommand : ICommand
{
    public string Name => "list-windows";
    public string Description => "List all Revit windows/dialogs";
    public string Usage => "list-windows (lw)";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var windows = FindWindowsWithName(window, null);
        var elements = OutputFormatter.FromElementList(windows);

        var result = new CommandResult
        {
            Command = "list-windows",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal" ? null : new { mainWindow = window.Name, windows = elements, count = elements.Count }
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class ListControlsCommand : ICommand
{
    public string Name => "list-controls";
    public string Description => "List controls in a window, optionally filtered by name";
    public string Usage => "list-controls (lc) [window-name-filter]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var filter = args.Length > 0 ? string.Join(" ", args) : null;
        var windows = FindWindowsWithName(window, filter);

        if (windows.Count == 0)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", filter ?? "", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var maxDepth = CoreSettings.Verbosity == "full" ? 5 : CoreSettings.Verbosity == "normal" ? 3 : 1;
        var elements = windows.Select(w => OutputFormatter.FromAutomationElement(w, 0, maxDepth)).ToList();

        var result = new CommandResult
        {
            Command = "list-controls",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal" ? null : new { windows = elements, count = elements.Count, filter }
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class FindCommand : ICommand
{
    public string Name => "find";
    public string Description => "Find controls matching a name and show their info";
    public string Usage => "find <control-name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var name = string.Join(" ", args);
        var results = FindControlsByName(window, name);

        if (results.Count == 0)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var elements = OutputFormatter.FromElementList(results);
        var result = new CommandResult
        {
            Command = "find",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { query = name, count = results.Count }
                : new { query = name, results = elements, count = results.Count }
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class InfoCommand : ICommand
{
    public string Name => "info";
    public string Description => "Show Revit window info";
    public string Usage => "info";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var rect = window.BoundingRectangle;
        var result = new CommandResult
        {
            Command = "info",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { name = window.Name, controlType = window.ControlType.ToString() }
                : new
                {
                    name = window.Name,
                    bounds = new { x = rect.X, y = rect.Y, width = rect.Width, height = rect.Height },
                    isEnabled = window.IsEnabled,
                    isOffscreen = window.IsOffscreen,
                    controlType = window.ControlType.ToString(),
                    automationId = window.AutomationId ?? ""
                }
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class DumpCommand : ICommand
{
    public string Name => "dump";
    public string Description => "Dump UIA tree to console or file (-f <path>)";
    public string Usage => "dump [depth] [-f <file>] [-t <control-type>] [-id <automation-id>]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var depth = 3;
        string? outputFile = null;
        string? filterType = null;
        string? filterId = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-f" && i + 1 < args.Length) outputFile = args[++i];
            else if (args[i] == "-t" && i + 1 < args.Length) filterType = args[++i];
            else if (args[i] == "-id" && i + 1 < args.Length) filterId = args[++i];
            else if (int.TryParse(args[i], out var d)) depth = d;
        }

        if (outputFile != null)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Revit window: \"{window.Name}\"");
            sb.AppendLine($"  Bounds: {window.BoundingRectangle}");
            sb.AppendLine($"  ControlType: {window.ControlType}");
            sb.AppendLine($"  AutomationId: \"{window.AutomationId}\"");
            sb.AppendLine();

            var lines = new List<string>();
            DumpTree(window, 0, depth, lines, filterType, filterId);
            foreach (var line in lines)
                sb.AppendLine(line);

            File.WriteAllText(outputFile, sb.ToString());
        }

        var treeDepth = CoreSettings.Verbosity == "full" ? depth : CoreSettings.Verbosity == "normal" ? Math.Min(depth, 2) : 0;
        var root = OutputFormatter.FromAutomationElement(window, 0, treeDepth);

        var result = new CommandResult
        {
            Command = "dump",
            Success = true,
            Data = CoreSettings.Verbosity == "minimal"
                ? new { name = window.Name, outputFile }
                : new { name = window.Name, tree = root, depth, filterType, filterId, outputFile }
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }

    private static void DumpTree(AutomationElement parent, int depth, int maxDepth, List<string> output, string? filterType, string? filterId)
    {
        if (depth > maxDepth) return;
        var indent = new string(' ', depth * 2);
        try
        {
            var index = 0;
            foreach (var c in SafeGetChildren(parent, depth == 0 ? 25000 : 5000))
            {
                try
                {
                    var name = c.Name ?? "";
                    var ctrlType = c.ControlType;
                    var enabled = c.IsEnabled;
                    var visible = c.IsOffscreen == false;
                    var autoId = c.AutomationId ?? "";

                    var typeOk = filterType == null || ctrlType.ToString().Contains(filterType, StringComparison.OrdinalIgnoreCase);
                    var idOk = filterId == null || autoId.Contains(filterId, StringComparison.OrdinalIgnoreCase);
                    var hasMatchingChild = false;

                    if (!typeOk || !idOk)
                    {
                        var childLines = new List<string>();
                        DumpTree(c, depth + 1, maxDepth, childLines, filterType, filterId);
                        if (childLines.Count > 0)
                            hasMatchingChild = true;
                        if (hasMatchingChild)
                        {
                            output.Add($"{indent}[{index}] [{ctrlType}] \"{Truncate(name, 60)}\" " +
                                $"enabled={enabled} visible={visible} id=\"{Truncate(autoId, 25)}\"");
                            output.AddRange(childLines);
                        }
                    }
                    else
                    {
                        output.Add($"{indent}[{index}] [{ctrlType}] \"{Truncate(name, 60)}\" " +
                            $"enabled={enabled} visible={visible} id=\"{Truncate(autoId, 25)}\" " +
                            $"w={c.BoundingRectangle.Width:F0}h={c.BoundingRectangle.Height:F0}");
                        DumpTree(c, depth + 1, maxDepth, output, filterType, filterId);
                    }

                    index++;
                }
                catch (Exception ex) { output.Add($"{indent}  [error: {ex.Message}]"); }
            }
            if (index == 0)
                output.Add($"{indent}(no children)");
        }
        catch (Exception ex)
        {
            output.Add($"{indent}[error: {ex.Message}]");
        }
    }
}

public class InspectCommand : ICommand
{
    public string Name => "inspect";
    public string Description => "Like Inspect.exe — explore element details. Usage: inspect [index-path] or inspect <name> or inspect -all";
    public string Usage => "inspect [index-path]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0 || args[0] == "-all")
        {
            var info = OutputFormatter.FromAutomationElement(window, 0, 1);
            var result = new CommandResult
            {
                Command = "inspect",
                Success = true,
                Data = CoreSettings.Verbosity == "minimal" ? new { name = window.Name } : new { element = info }
            };
            if (CoreSettings.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(window);
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }

        var target = FollowPath(window, args);
        if (target == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", string.Join(" ", args), null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var elementInfo = OutputFormatter.FromAutomationElement(target, 0, 0);
        try
        {
            var children = SafeGetChildren(target, 25000);
            var rect = target.BoundingRectangle;

            var result = new CommandResult
            {
                Command = "inspect",
                Success = true,
                Data = CoreSettings.Verbosity == "minimal"
                    ? new { name = target.Name, controlType = target.ControlType.ToString() }
                    : new
                    {
                        element = elementInfo,
                        helpText = SafeGet(() => target.HelpText),
                        itemStatus = SafeGet(() => target.ItemStatus),
                        childrenCount = children.Length,
                        children = CoreSettings.Verbosity == "full"
                            ? children.Take(20).Select((c, i) => new
                            {
                                index = i,
                                controlType = c.ControlType.ToString(),
                                name = SafeGet(() => c.Name),
                                automationId = SafeGet(() => c.AutomationId),
                                bounds = new { x = c.BoundingRectangle.X, y = c.BoundingRectangle.Y, w = c.BoundingRectangle.Width, h = c.BoundingRectangle.Height }
                            }).ToList()
                            : null
                    }
            };
            if (CoreSettings.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(window);
            Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch
        {
            Console.Write(OutputFormatter.FormatError("InspectError", string.Join(" ", args), null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }

    private static AutomationElement? FollowPath(AutomationElement root, string[] pathSegments)
    {
        var current = root;
        foreach (var seg in pathSegments)
        {
            if (int.TryParse(seg, out var idx))
            {
                var children = SafeGetChildren(current, 25000);
                if (idx < 0 || idx >= children.Length) return null;
                current = children[idx];
            }
            else
            {
                var found = FindFirstEnabledVisible(current, seg);
                if (found == null) return null;
                current = found;
            }
        }
        return current;
    }

    private static string SafeGet(Func<string?> f) { try { return f() ?? ""; } catch { return ""; } }
}
