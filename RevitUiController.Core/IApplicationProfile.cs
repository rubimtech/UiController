namespace UiController.Core;

public interface IApplicationProfile
{
    string Name { get; }
    string ProcessName { get; }
    string[] ExecutablePaths { get; }
    string? PipeName { get; }
    string ConfigDirectory { get; }
    string UiMapFileName { get; }
    string LocaleFileName { get; }
    string TemplatesDirectory { get; }
    string ScriptsDirectory { get; }
    string[] KnownVersions { get; }
    string DetectVersionFromTitle(string title);
    string DetectVersionFromFileVersion(string fileVersion);
    string BuildLlmSystemPrompt(string appName);
}
