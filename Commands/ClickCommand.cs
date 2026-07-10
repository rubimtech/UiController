using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

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

        var found = FindByAutoIdInRoot(revitWindow, query);
        if (found == null)
            found = FindFirstEnabledVisible(revitWindow, query);

        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", query, null, Program.GlobalOptions));
            return 1;
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        var clicked = TryClick(found, query);
        if (!clicked)
        {
            Console.Write(OutputFormatter.FormatError("ClickFailed", query, null, Program.GlobalOptions));
            return 1;
        }
        var after = OutputFormatter.CaptureState(revitWindow);
        var diff = OutputFormatter.ComputeDiff(before, after);

        var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
        var result = new CommandResult
        {
            Command = "click",
            Success = true,
            Diff = diff,
            Data = Program.Verbosity == "minimal" ? null : new { target = query },
            DurationMs = elapsed
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return 0;
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
