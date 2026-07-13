using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using UiController.Core;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Commands;

public class ClickCommand : ICommand
{
    public string Name => "click";
    public string Description => "Click a button/control by name";
    public string Usage => "click <button-name> or click-id <automation-id>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var query = string.Join(" ", args);
        var start = DateTime.UtcNow;

        var request = CommandResultStore.CurrentRequest;
        var useChain = request?.Retry == true || !string.IsNullOrEmpty(request?.Strategy);

        if (useChain)
        {
            var strategies = ParseStrategies(request?.Strategy);
            var (success, method, attempts) = await ClickFallbackChain.ExecuteAsync(revitWindow, query, strategies, ct);
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;

            if (!success)
            {
                Console.Write(OutputFormatter.FormatError("NotFound", query,
                [
                    "All strategies failed",
                    ..attempts.Select(a => $"{a.Name}: {(a.Success ? "ok" : $"fail ({a.Error ?? "no match"})")} ({a.DurationMs}ms)")
                ], options: Program.GlobalOptions));
                return 1;
            }

            var after = OutputFormatter.CaptureState(revitWindow);
            var result = new CommandResult
            {
                Command = "click",
                Success = true,
                Data = new
                {
                    target = query,
                    method,
                    attempts = attempts.Select(a => new
                    {
                        strategy = a.Name,
                        success = a.Success,
                        durationMs = a.DurationMs
                    }).ToList()
                },
                DurationMs = elapsed
            };
            if (Program.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
            Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
            return 0;
        }

        var found = FindByAutoIdInRoot(revitWindow, query);
        if (found == null)
            found = FindFirstEnabledVisible(revitWindow, query);

        if (found == null)
        {
            var similar = AutomationHelper.FindSimilarElementNames(revitWindow, query);
            var suggestions = new List<string>
            {
                "Try 'ai-find \"" + query + "\"' for multi-strategy search",
                "Try 'list-controls' to see available elements"
            };
            Console.Write(OutputFormatter.FormatError("NotFound", query, suggestions, options: Program.GlobalOptions, availableElements: similar));
            return 1;
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        var clicked = TryClick(found, query);
        if (!clicked)
        {
            Console.Write(OutputFormatter.FormatError("ClickFailed", query, null, Program.GlobalOptions));
            return 1;
        }
        var after2 = OutputFormatter.CaptureState(revitWindow);
        var diff = OutputFormatter.ComputeDiff(before, after2);

        var elapsed2 = (DateTime.UtcNow - start).TotalMilliseconds;
        var result2 = new CommandResult
        {
            Command = "click",
            Success = true,
            Diff = diff,
            Data = Program.Verbosity == "minimal" ? null : new { target = query },
            DurationMs = elapsed2
        };
        if (Program.IsScreenshot)
            result2.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result2, Program.GlobalOptions));
        return 0;
    }

    private static string[]? ParseStrategies(string? strategy)
    {
        if (string.IsNullOrEmpty(strategy)) return null;
        if (strategy.Contains(','))
            return strategy.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (ClickFallbackChain.DefaultChain.Contains(strategy))
            return new[] { strategy };
        return null;
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
}
