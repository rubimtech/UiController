namespace RevitUiController.Core;

public class ConfigModel
{
    public Dictionary<string, ProfileConfig> Profiles { get; set; } = new();
    public DefaultsConfig Defaults { get; set; } = new();
}

public class ProfileConfig
{
    public string ProcessName { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? PipeName { get; set; }
    public List<string> ExecutablePaths { get; set; } = new();
    public string? ConfigDirectory { get; set; }
    public string? UiMap { get; set; }
    public string? ScriptsDir { get; set; }
    public string? TemplatesDir { get; set; }
    public List<string> KnownYears { get; set; } = new();
    public string? LlmPrompt { get; set; }
}

public class DefaultsConfig
{
    public string Profile { get; set; } = "revit";
    public int ConnectTimeout { get; set; } = 30;
    public string Verbosity { get; set; } = "normal";
}
