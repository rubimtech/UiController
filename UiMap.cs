using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RevitUiController;

public class UiMapEntry
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? Tab { get; set; }
    public string? ParentPath { get; set; }
    public Dictionary<string, UiMapEntry>? Versions { get; set; }
    public string[]? Fallbacks { get; set; }
}

public class UiMapConfig
{
    public Dictionary<string, UiMapEntry> Entries { get; set; } = new();
    public Dictionary<string, string?> LocaleMap { get; set; } = new();
}

public class SelectorCandidate
{
    public string? AutomationId { get; set; }
    public string? Name { get; set; }
    public string? Tab { get; set; }
    public string? ParentPath { get; set; }
    public string[]? Fallbacks { get; set; }
    public string Source { get; set; } = "entry";
}

public static class UiMap
{
    private static UiMapConfig? _config;
    private static readonly object _lock = new();
    private static readonly ISerializer Serializer = new SerializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .Build();

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly string[] DefaultSearchPaths =
    [
        "./uimap.yaml",
        "./uimap.yml",
        "./config/uimap.yaml",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReVibe", "UiController", "uimap.yaml")
    ];

    public static UiMapConfig? Current => _config;
    public static bool IsLoaded => _config != null;
    public static string? CurrentPath { get; private set; }
    public static int EntryCount => _config?.Entries.Count ?? 0;

    public static bool Load(string path)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            var yaml = File.ReadAllText(path);
            var config = Deserializer.Deserialize<UiMapConfig>(yaml);
            if (config == null)
                return false;

            lock (_lock)
            {
                _config = config;
                CurrentPath = Path.GetFullPath(path);
            }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryLoadDefault()
    {
        foreach (var searchPath in DefaultSearchPaths)
        {
            var fullPath = Path.GetFullPath(searchPath);
            if (Load(fullPath))
                return true;
        }
        return false;
    }

    public static void Save(string path)
    {
        var config = _config ?? new UiMapConfig();
        var yaml = Serializer.Serialize(config);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, yaml);
        lock (_lock)
        {
            _config = config;
            CurrentPath = Path.GetFullPath(path);
        }
    }

    public static List<SelectorCandidate> Resolve(string logicalName, int? revitYear = null)
    {
        var results = new List<SelectorCandidate>();
        if (_config == null)
            return results;

        if (!_config.Entries.TryGetValue(logicalName, out var entry))
            return results;

        UiMapEntry resolved = entry;

        if (revitYear.HasValue && entry.Versions != null)
        {
            var yearStr = revitYear.Value.ToString();
            if (entry.Versions.TryGetValue(yearStr, out var versionOverride))
                resolved = versionOverride;
        }

        var source = revitYear.HasValue && entry.Versions?.ContainsKey(revitYear.Value.ToString()) == true
            ? $"entry (version {revitYear})"
            : "entry";

        if (!string.IsNullOrEmpty(resolved.AutomationId) || !string.IsNullOrEmpty(resolved.Name) || !string.IsNullOrEmpty(resolved.Tab))
        {
            results.Add(new SelectorCandidate
            {
                AutomationId = resolved.AutomationId,
                Name = resolved.Name ?? logicalName,
                Tab = resolved.Tab,
                ParentPath = resolved.ParentPath,
                Fallbacks = resolved.Fallbacks,
                Source = source
            });
        }

        if (resolved.Fallbacks != null)
        {
            foreach (var fb in resolved.Fallbacks)
            {
                results.Add(new SelectorCandidate
                {
                    AutomationId = null,
                    Name = fb,
                    Tab = resolved.Tab,
                    ParentPath = resolved.ParentPath,
                    Source = $"fallback: {fb}"
                });
            }
        }

        if (!string.IsNullOrEmpty(entry.Name) && entry.Name != resolved.Name)
        {
            results.Add(new SelectorCandidate
            {
                AutomationId = resolved.AutomationId ?? entry.AutomationId,
                Name = entry.Name,
                Tab = entry.Tab ?? resolved.Tab,
                ParentPath = entry.ParentPath ?? resolved.ParentPath,
                Fallbacks = entry.Fallbacks,
                Source = "entry (base)"
            });
        }

        return results;
    }

    public static UiMapEntry? GetEntry(string logicalName)
    {
        if (_config == null) return null;
        return _config.Entries.TryGetValue(logicalName, out var entry) ? entry : null;
    }

    public static void Register(string logicalName, UiMapEntry entry)
    {
        lock (_lock)
        {
            _config ??= new UiMapConfig();
            _config.Entries[logicalName] = entry;
        }
    }

    public static Dictionary<string, UiMapEntry> GetAllEntries()
    {
        return _config?.Entries ?? new Dictionary<string, UiMapEntry>();
    }

    public static Dictionary<string, UiMapEntry> FindEntries(string? filter = null)
    {
        if (_config == null)
            return new Dictionary<string, UiMapEntry>();

        if (string.IsNullOrWhiteSpace(filter))
            return new Dictionary<string, UiMapEntry>(_config.Entries);

        var result = new Dictionary<string, UiMapEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in _config.Entries)
        {
            if (kvp.Key.Contains(filter, StringComparison.OrdinalIgnoreCase))
                result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    public static string? ResolveLocale(string name)
    {
        if (_config?.LocaleMap == null)
            return null;
        return _config.LocaleMap.TryGetValue(name, out var en) ? en : null;
    }

    public static void Unload()
    {
        lock (_lock)
        {
            _config = null;
            CurrentPath = null;
        }
    }
}
