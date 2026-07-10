using FlaUI.Core.AutomationElements;

namespace RevitUiController;

[Obsolete("Use RetryPolicy instead. Will be removed in a future version.")]
public static class FlakyRetry
{
    public static T? Retry<T>(Func<T?> action, int maxAttempts = 3, int initialDelayMs = 500, Func<T?, bool>? successCheck = null) where T : class
    {
        return RetryPolicy.RetryAsync(
            () => Task.FromResult(action()),
            maxAttempts,
            initialDelayMs,
            RetryPolicy.BackoffMode.Exponential,
            successCheck
        ).GetAwaiter().GetResult();
    }

    public static bool RetryAction(Action action, int maxAttempts = 3, int initialDelayMs = 500)
    {
        return RetryPolicy.RetryActionAsync(
            () => { action(); return Task.CompletedTask; },
            maxAttempts,
            initialDelayMs,
            RetryPolicy.BackoffMode.Exponential
        ).GetAwaiter().GetResult();
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
