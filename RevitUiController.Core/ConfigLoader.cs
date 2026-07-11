using System.IO;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RevitUiController.Core;

public static class ConfigLoader
{
    private static readonly string[] SearchPaths =
    [
        "./config.yaml",
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "UiController", "config.yaml")
    ];

    public static ConfigModel Load()
    {
        foreach (var path in SearchPaths)
        {
            if (File.Exists(path))
            {
                var yaml = File.ReadAllText(path);
                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();
                return deserializer.Deserialize<ConfigModel>(yaml) ?? new ConfigModel();
            }
        }
        return new ConfigModel();
    }

    public static IApplicationProfile CreateProfile(string name, ProfileConfig cfg)
    {
        var processName = string.IsNullOrEmpty(cfg.ProcessName) ? name : cfg.ProcessName;
        return new GenericProfile(processName);
    }
}
