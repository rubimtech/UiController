using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

namespace RevitUiController.Commands;

public class StatusBarCommand : ICommand
{
    public string Name => "statusbar";
    public string Description => "Read Revit status bar text";
    public string Usage => "statusbar";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var text = ReadStatusBarText(revitWindow);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "statusbar",
            Success = true,
            Data = new { text }
        }, Program.GlobalOptions));
        return 0;
    }

    private static string ReadStatusBarText(AutomationElement revitWindow)
    {
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(revitWindow);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    var autoId = el.AutomationId ?? "";
                    var name = el.Name ?? "";
                    if (autoId.Contains("StatusBar", StringComparison.OrdinalIgnoreCase) ||
                        autoId.Contains("statusBar", StringComparison.OrdinalIgnoreCase))
                    {
                        return name;
                    }
                    if (name.Contains("StatusBar", StringComparison.OrdinalIgnoreCase) ||
                        name.Contains("statusBar", StringComparison.OrdinalIgnoreCase))
                    {
                        return CollectAllText(el);
                    }
                    foreach (var c in SafeGetChildren(el, 2000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }

        try
        {
            foreach (var c in SafeGetChildren(revitWindow, 10000))
            {
                try
                {
                    if (c.ControlType == ControlType.StatusBar)
                        return c.Name ?? CollectAllText(c);
                }
                catch { }
            }
        }
        catch { }
        return "";
    }

    private static string CollectAllText(AutomationElement parent)
    {
        var texts = new List<string>();
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(parent);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    var name = el.Name ?? "";
                    if (!string.IsNullOrEmpty(name) && !name.StartsWith("UIFramework") && !name.StartsWith("Autodesk"))
                        texts.Add(name);
                    foreach (var c in SafeGetChildren(el, 1000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }
        return string.Join(" ", texts.Distinct());
    }
}

public class WaitProgressCommand : ICommand
{
    public string Name => "wait-progress";
    public string Description => "Wait for Revit progress bar to complete: wait-progress [timeout-s]";
    public string Usage => "wait-progress [timeout-s]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var timeout = args.Length > 0 && int.TryParse(args[0], out var t) ? t * 1000 : 60000;

        if (Program.EventService is { IsListening: true })
        {
            var completed = await Program.EventService.WaitForProgressAsync(timeout, ct);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "wait-progress",
                Success = completed,
                Error = completed ? null : $"Progress did not complete within {timeout / 1000}s",
                Data = completed ? new { action = "completed" } : null
            }, Program.GlobalOptions));
            return completed ? 0 : 1;
        }

        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        var progressDetected = false;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var progress = FindProgressBar(revitWindow);
                if (progress == null)
                {
                    if (progressDetected)
                    {
                        Console.Write(OutputFormatter.FormatResult(new CommandResult
                        {
                            Command = "wait-progress",
                            Success = true,
                            Data = new { action = "completed", waitedMs = timeout - (int)(deadline - DateTime.UtcNow).TotalMilliseconds }
                        }, Program.GlobalOptions));
                        return 0;
                    }
                    await Task.Delay(500, ct);
                    continue;
                }

                progressDetected = true;

                try
                {
                    var value = progress.Patterns.RangeValue.Pattern?.Value;
                    if (value >= 100)
                    {
                        Console.Write(OutputFormatter.FormatResult(new CommandResult
                        {
                            Command = "wait-progress",
                            Success = true,
                            Data = new { action = "completed", finalValue = value }
                        }, Program.GlobalOptions));
                        return 0;
                    }
                }
                catch { }
            }
            catch { }
            await Task.Delay(500, ct);
        }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wait-progress",
            Success = false,
            Error = $"Progress did not complete within {timeout / 1000}s"
        }, Program.GlobalOptions));
        return 1;
    }

    private static AutomationElement? FindProgressBar(AutomationElement root)
    {
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(root);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    if (el.ControlType == ControlType.ProgressBar)
                        return el;
                    foreach (var c in SafeGetChildren(el, 2000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}
