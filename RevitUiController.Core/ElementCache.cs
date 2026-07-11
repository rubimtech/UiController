using System.Collections.Concurrent;
using FlaUI.Core.AutomationElements;

namespace UiController.Core;

public static class ElementCache
{
    private static readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromSeconds(5);

    private class CacheEntry
    {
        public AutomationElement? Element;
        public string Name = "";
        public string AutomationId = "";
        public DateTime CachedAt;
        public AutomationElement? Root;
    }

    public static void Add(string key, AutomationElement element, AutomationElement? root = null, TimeSpan? ttl = null)
    {
        try
        {
            _cache[key] = new CacheEntry
            {
                Element = element,
                Name = element.Name ?? "",
                AutomationId = element.AutomationId ?? "",
                CachedAt = DateTime.UtcNow,
                Root = root
            };
        }
        catch { }
    }

    public static AutomationElement? Get(string key)
    {
        if (!_cache.TryGetValue(key, out var entry)) return null;
        if (DateTime.UtcNow - entry.CachedAt > DefaultTtl)
        {
            _cache.TryRemove(key, out _);
            if (entry.Root != null)
            {
                var refreshed = AutomationHelper.FindFirstEnabledVisible(entry.Root, entry.Name);
                if (refreshed != null)
                {
                    Add(key, refreshed, entry.Root);
                    return refreshed;
                }
            }
            return null;
        }
        try
        {
            var enabled = entry.Element?.IsEnabled;
            if (enabled == null || enabled == false)
            {
                _cache.TryRemove(key, out _);
                return null;
            }
        }
        catch
        {
            _cache.TryRemove(key, out _);
            return null;
        }
        return entry.Element;
    }

    public static void Invalidate(string key)
    {
        _cache.TryRemove(key, out _);
    }

    public static void InvalidateAll()
    {
        _cache.Clear();
    }

    public static int Count => _cache.Count;
}
