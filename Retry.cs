using FlaUI.Core.AutomationElements;

namespace RevitUiController;

public static class Retry
{
    public static AutomationElement? WaitForElement(AutomationElement root, string name, int timeoutMs = 10000, int intervalMs = 500)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var found = AutomationHelper.FindFirstEnabledVisible(root, name);
            if (found != null) return found;
            Thread.Sleep(intervalMs);
        }
        return null;
    }

    public static AutomationElement? WaitForElementByAutoId(AutomationElement root, string automationId, int timeoutMs = 10000, int intervalMs = 500)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var found = FindByAutoId(root, automationId);
            if (found != null) return found;
            Thread.Sleep(intervalMs);
        }
        return null;
    }

    public static AutomationElement? WaitForDialog(AutomationElement root, string title, int timeoutMs = 15000, int intervalMs = 300)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var dialogs = AutomationHelper.FindActiveDialogs(root);
            var match = dialogs.FirstOrDefault(d =>
                (d.Name ?? "").Contains(title, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
            Thread.Sleep(intervalMs);
        }
        return null;
    }

    public static bool WaitForDialogClose(AutomationElement root, string title, int timeoutMs = 15000, int intervalMs = 300)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var dialogs = AutomationHelper.FindActiveDialogs(root);
            var match = dialogs.Any(d =>
                (d.Name ?? "").Contains(title, StringComparison.OrdinalIgnoreCase));
            if (!match) return true;
            Thread.Sleep(intervalMs);
        }
        return false;
    }

    public static bool WaitForCondition(Func<bool> condition, int timeoutMs = 10000, int intervalMs = 200)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            Thread.Sleep(intervalMs);
        }
        return condition();
    }

    private static AutomationElement? FindByAutoId(AutomationElement root, string autoId)
    {
        try
        {
            foreach (var c in AutomationHelper.SafeGetChildren(root, 5000))
            {
                try
                {
                    if ((c.AutomationId ?? "").Equals(autoId, StringComparison.OrdinalIgnoreCase))
                        return c;
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}
