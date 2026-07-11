namespace UiController.Core;

public static class SafeExtensions
{
    public static T? Safe<T>(this Func<T> func, string context = "") where T : class
    {
        try { return func(); }
        catch (Exception ex) { LoggingService.Warn("Safe", $"{context}: {ex.Message}"); return null; }
    }

    public static T SafeOrDefault<T>(this Func<T> func, T defaultValue = default, string context = "") where T : struct
    {
        try { return func(); }
        catch (Exception ex) { LoggingService.Warn("Safe", $"{context}: {ex.Message}"); return defaultValue; }
    }

    public static void Safe(this Action action, string context = "")
    {
        try { action(); }
        catch (Exception ex) { LoggingService.Warn("Safe", $"{context}: {ex.Message}"); }
    }

    public static T? SafeGet<T>(this Func<T> func, string context = "") where T : class
    {
        try { return func(); }
        catch (Exception ex) { LoggingService.Warn("Safe", $"{context}: {ex.Message}"); return null; }
    }
}
