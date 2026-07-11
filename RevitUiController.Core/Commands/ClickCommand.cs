using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace UiController.Core.Commands;

public class ClickCommand : ICommand
{
    public string Name => "click";
    public string Description => "Click a button/control by name";
    public string Usage => "click <button-name> or click-id <automation-id>";

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var query = string.Join(" ", args);
        var start = DateTime.UtcNow;

        var found = FindByAutoIdInRoot(window, query);
        if (found == null)
            found = FindFirstEnabledVisible(window, query);

        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", query, null, CoreSettings.GlobalOptions));
            return 1;
        }

        var before = OutputFormatter.CaptureState(window);
        var clicked = TryClick(found, query);
        if (!clicked)
        {
            Console.Write(OutputFormatter.FormatError("ClickFailed", query, null, CoreSettings.GlobalOptions));
            return 1;
        }
        var after = OutputFormatter.CaptureState(window);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
        var result = new CommandResult
        {
            Command = "click",
            Success = true,
            Diff = diff,
            Data = CoreSettings.Verbosity == "minimal" ? null : new { target = query },
            DurationMs = elapsed
        };
        if (CoreSettings.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(window);
        Console.Write(OutputFormatter.FormatResult(result, CoreSettings.GlobalOptions));
        return 0;
    }

    private static AutomationElement? FindByAutoIdInRoot(AutomationElement window, string nameOrId)
    {
        var children = SafeGetChildren(window, 60000);

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
