using System.Collections.Concurrent;

namespace UiController.Core;

public static class LlmVisionCache
{
    private static readonly ConcurrentDictionary<string, CachedElement> _cache = new(StringComparer.OrdinalIgnoreCase);

    public class CachedElement
    {
        public string Name { get; set; } = "";
        public string? AutomationId { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public DateTime LastSeen { get; set; }
        public string FoundBy { get; set; } = "uia";
    }

    public static void Add(string name, CachedElement element)
    {
        _cache[name] = element;
    }

    public static CachedElement? Get(string name)
    {
        _cache.TryGetValue(name, out var entry);
        return entry;
    }
}
