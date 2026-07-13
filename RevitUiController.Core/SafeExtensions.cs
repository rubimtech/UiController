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

    public static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;
        a = a.ToLowerInvariant();
        b = b.ToLowerInvariant();
        var n = b.Length;
        var prev = new int[n + 1];
        var curr = new int[n + 1];
        for (int j = 0; j <= n; j++) prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (int j = 1; j <= n; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(Math.Min(curr[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }
        return prev[n];
    }

    public static List<string> FindSimilar(string query, IEnumerable<string> candidates, int maxResults = 5)
    {
        return candidates
            .Select(c => new { Name = c, Distance = LevenshteinDistance(query, c) })
            .Where(x => x.Distance <= Math.Max(3, query.Length / 2))
            .OrderBy(x => x.Distance)
            .ThenBy(x => x.Name)
            .Take(maxResults)
            .Select(x => x.Name)
            .ToList();
    }
}
