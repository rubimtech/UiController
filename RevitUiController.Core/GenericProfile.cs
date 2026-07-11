using System.IO;

namespace RevitUiController.Core;

public class GenericProfile : IApplicationProfile
{
    public string Name { get; }
    public string ProcessName { get; }
    public string[] ExecutablePaths => [];
    public string? PipeName => null;
    public string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Name, "UiController");
    public string UiMapFileName => "uimap.yaml";
    public string LocaleFileName => "locale.yaml";
    public string TemplatesDirectory => "templates";
    public string ScriptsDirectory => "scripts";
    public string[] KnownVersions => [];

    public GenericProfile(string processName)
    {
        ProcessName = processName;
        Name = processName;
    }

    public string DetectVersionFromTitle(string title) => "unknown";
    public string DetectVersionFromFileVersion(string fileVersion)
    {
        try
        {
            var version = new Version(fileVersion);
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch { return "unknown"; }
    }
    public string BuildLlmSystemPrompt(string appName) =>
        $"Look at this screenshot of {appName}. Identify the UI elements visible in the image.";
}
