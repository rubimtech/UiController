using FlaUI.Core.AutomationElements;

namespace RevitUiController;

public static class Retry
{
    public static async Task<AutomationElement?> WaitForElement(AutomationElement root, string name, int timeoutMs = 10000, int intervalMs = 500, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var found = AutomationHelper.FindFirstEnabledVisible(root, name);
            if (found != null) return found;
            await Task.Delay(intervalMs, ct);
        }
        return null;
    }

    public static async Task<AutomationElement?> WaitForElementByAutoId(AutomationElement root, string automationId, int timeoutMs = 10000, int intervalMs = 500, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var found = FindByAutoId(root, automationId);
            if (found != null) return found;
            await Task.Delay(intervalMs, ct);
        }
        return null;
    }

    public static async Task<AutomationElement?> WaitForDialog(AutomationElement root, string title, int timeoutMs = 15000, int intervalMs = 300, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var dialogs = AutomationHelper.FindActiveDialogs(root);
            var match = dialogs.FirstOrDefault(d =>
                (d.Name ?? "").Contains(title, StringComparison.OrdinalIgnoreCase));
            if (match != null) return match;
            await Task.Delay(intervalMs, ct);
        }
        return null;
    }

    public static async Task<bool> WaitForDialogClose(AutomationElement root, string title, int timeoutMs = 15000, int intervalMs = 300, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var dialogs = AutomationHelper.FindActiveDialogs(root);
            var match = dialogs.Any(d =>
                (d.Name ?? "").Contains(title, StringComparison.OrdinalIgnoreCase));
            if (!match) return true;
            await Task.Delay(intervalMs, ct);
        }
        return false;
    }

    public static async Task<bool> WaitForCondition(Func<bool> condition, int timeoutMs = 10000, int intervalMs = 200, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            if (condition()) return true;
            await Task.Delay(intervalMs, ct);
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
                catch (Exception ex) { LoggingService.Warn("Safe", $"FindByAutoId inner: {ex.Message}"); }
            }
        }
        catch (Exception ex) { LoggingService.Warn("Safe", $"FindByAutoId outer: {ex.Message}"); }
        return null;
    }
}

public static class RetryPolicy
{
    public enum BackoffMode { Fixed, Exponential }

    public static async Task<T?> RetryAsync<T>(
        Func<Task<T?>> action,
        int maxAttempts = 3,
        int initialDelayMs = 500,
        BackoffMode mode = BackoffMode.Exponential,
        Func<T?, bool>? successCheck = null,
        CancellationToken ct = default) where T : class
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await action();
                if (successCheck == null || successCheck(result))
                    return result;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                LoggingService.Warn("RetryPolicy", $"Attempt {attempt}/{maxAttempts} failed: {ex.Message}");
            }

            if (attempt < maxAttempts)
            {
                var delay = mode == BackoffMode.Exponential
                    ? initialDelayMs * (1 << (attempt - 1))
                    : initialDelayMs;
                await Task.Delay(delay, ct);
            }
        }
        return null;
    }

    public static async Task<bool> RetryActionAsync(
        Func<Task> action,
        int maxAttempts = 3,
        int initialDelayMs = 500,
        BackoffMode mode = BackoffMode.Exponential,
        CancellationToken ct = default)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await action();
                return true;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                LoggingService.Warn("RetryPolicy", $"Attempt {attempt}/{maxAttempts} failed: {ex.Message}");
            }

            if (attempt < maxAttempts)
            {
                var delay = mode == BackoffMode.Exponential
                    ? initialDelayMs * (1 << (attempt - 1))
                    : initialDelayMs;
                await Task.Delay(delay, ct);
            }
        }
        return false;
    }
}
