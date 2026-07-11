using System.Collections.Concurrent;
using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RevitUiController.Core;

public class LocaleYamlConfig
{
    public Dictionary<string, string> Entries { get; set; } = new();
}

public static class LocaleMap
{
    private static readonly ConcurrentDictionary<string, string> RuToEn = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string> EnToRu = new(StringComparer.OrdinalIgnoreCase);
    private static bool _initialized;
    private static readonly object _lock = new();
    public static string? CurrentPath { get; private set; }

    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly string[] DefaultSearchPaths =
    [
        "./locale.yaml",
        "./config/locale.yaml",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReVibe", "UiController", "locale.yaml")
    ];

    public static void Initialize()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            if (!TryLoadFromYaml())
                LoadHardcoded();

            _initialized = true;
        }
    }

    private static bool TryLoadFromYaml()
    {
        foreach (var searchPath in DefaultSearchPaths)
        {
            var fullPath = Path.GetFullPath(searchPath);
            if (!File.Exists(fullPath))
                continue;

            try
            {
                var yaml = File.ReadAllText(fullPath);
                var config = Deserializer.Deserialize<LocaleYamlConfig>(yaml);
                if (config?.Entries == null || config.Entries.Count == 0)
                    continue;

                foreach (var kvp in config.Entries)
                {
                    RuToEn[kvp.Key] = kvp.Value;
                    EnToRu[kvp.Value] = kvp.Key;
                }

                CurrentPath = fullPath;
                return true;
            }
            catch
            {
                continue;
            }
        }
        return false;
    }

    private static void LoadHardcoded()
    {
        Add("Стена", "Wall");
        Add("Дверь", "Door");
        Add("Окно", "Window");
        Add("Перекрытие", "Floor");
        Add("Крыша", "Roof");
        Add("Колонна", "Column");
        Add("Балка", "Beam");
        Add("Лестница", "Stair");
        Add("Потолок", "Ceiling");
        Add("Ограждение", "Railing");
        Add("Труба", "Pipe");
        Add("Воздуховод", "Duct");
        Add("Кабель", "Cable");
        Add("Архитектура", "Architecture");
        Add("Конструкции", "Structure");
        Add("Системы", "Systems");
        Add("Вставка", "Insert");
        Add("Аннотации", "Annotate");
        Add("Вид", "View");
        Add("Управление", "Manage");
        Add("Модификация", "Modify");
        Add("Совместная работа", "Collaborate");
    }

    private static void Add(string ru, string en)
    {
        RuToEn[ru] = en;
        EnToRu[en] = ru;
    }

    public static string? ToEnglish(string russian)
    {
        Initialize();
        return RuToEn.TryGetValue(russian, out var en) ? en : null;
    }

    public static string? ToRussian(string english)
    {
        Initialize();
        return EnToRu.TryGetValue(english, out var ru) ? ru : null;
    }

    public static string Normalize(string name)
    {
        Initialize();
        if (RuToEn.TryGetValue(name, out var en)) return en;
        return name;
    }

    public static List<string> GetAlternatives(string name)
    {
        Initialize();
        var results = new List<string>();
        if (RuToEn.TryGetValue(name, out var en)) results.Add(en);
        if (EnToRu.TryGetValue(name, out var ru)) results.Add(ru);
        return results;
    }
}
