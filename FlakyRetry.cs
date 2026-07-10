using FlaUI.Core.AutomationElements;

namespace RevitUiController;

public static class FlakyRetry
{
    public static T? Retry<T>(Func<T?> action, int maxAttempts = 3, int initialDelayMs = 500, Func<T?, bool>? successCheck = null) where T : class
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = action();
                if (successCheck == null || successCheck(result))
                    return result;

                if (attempt < maxAttempts)
                {
                    var delay = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                    Console.Error.WriteLine($"[FlakyRetry] Attempt {attempt}/{maxAttempts} failed, retrying in {delay}ms...");
                    Thread.Sleep(delay);
                }
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts) throw;
                var delay = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                Console.Error.WriteLine($"[FlakyRetry] Attempt {attempt}/{maxAttempts} threw: {ex.Message}, retrying in {delay}ms...");
                Thread.Sleep(delay);
            }
        }
        return null;
    }

    public static bool RetryAction(Action action, int maxAttempts = 3, int initialDelayMs = 500)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception ex)
            {
                if (attempt >= maxAttempts)
                {
                    Console.Error.WriteLine($"[FlakyRetry] All {maxAttempts} attempts failed: {ex.Message}");
                    return false;
                }
                var delay = initialDelayMs * (int)Math.Pow(2, attempt - 1);
                Console.Error.WriteLine($"[FlakyRetry] Attempt {attempt}/{maxAttempts} failed: {ex.Message}, retrying in {delay}ms...");
                Thread.Sleep(delay);
            }
        }
        return false;
    }

    public static AutomationElement? RetryFind(AutomationElement root, string name, int maxAttempts = 3, int initialDelayMs = 500)
    {
        return Retry(
            () => AutomationHelper.FindFirstEnabledVisible(root, name),
            maxAttempts,
            initialDelayMs,
            result => result != null
        );
    }

    public static AutomationElement? RetryDialog(AutomationElement root, string title, int maxAttempts = 3, int initialDelayMs = 500)
    {
        return Retry(
            () => AutomationHelper.FindActiveDialogs(root)
                .FirstOrDefault(d => (d.Name ?? "").Contains(title, StringComparison.OrdinalIgnoreCase)),
            maxAttempts,
            initialDelayMs,
            result => result != null
        );
    }
}
