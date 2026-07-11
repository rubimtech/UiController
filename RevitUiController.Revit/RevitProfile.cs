using System.IO;
using RevitUiController.Core;

namespace RevitUiController.Revit;

public class RevitProfile : IApplicationProfile
{
    public string Name => "Revit";
    public string ProcessName => "Revit";
    public string[] ExecutablePaths => [
        @"C:\Program Files\Autodesk\Revit 2026\Revit.exe",
        @"C:\Program Files\Autodesk\Revit 2025\Revit.exe",
        @"C:\Program Files\Autodesk\Revit 2024\Revit.exe",
        @"C:\Program Files\Autodesk\Revit 2027\Revit.exe",
    ];
    public string? PipeName => "ReVibe";
    public string ConfigDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ReVibe", "UiController");
    public string UiMapFileName => "uimap.yaml";
    public string LocaleFileName => "locale.yaml";
    public string TemplatesDirectory => "templates";
    public string ScriptsDirectory => "scripts";
    public string[] KnownVersions => ["2022", "2023", "2024", "2025", "2026", "2027"];

    public string DetectVersionFromTitle(string title)
    {
        var year = RevitVersionProfile.DetectYearFromTitle(title);
        return year > 0 ? year.ToString() : "unknown";
    }

    public string DetectVersionFromFileVersion(string fileVersion)
    {
        var year = RevitVersionProfile.DetectFromFileVersion(fileVersion);
        return year.HasValue ? year.Value.ToString() : "unknown";
    }

    public string BuildLlmSystemPrompt(string appName) =>
        "Look at this screenshot of Autodesk Revit. Identify the UI elements, ribbon tabs, panels, and dialog boxes visible in the image.";
}
