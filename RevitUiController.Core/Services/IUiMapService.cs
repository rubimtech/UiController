namespace UiController.Core.Services;

public interface IUiMapService
{
    bool Load(string path);
    bool TryLoadDefault();
    void Save(string path);
    List<SelectorCandidate> Resolve(string logicalName, int? revitYear = null);
    UiMapEntry? GetEntry(string logicalName);
    void Register(string logicalName, UiMapEntry entry);
    Dictionary<string, UiMapEntry> GetAllEntries();
    Dictionary<string, UiMapEntry> FindEntries(string? filter = null);
    string? ResolveLocale(string name);
    void Unload();
    bool IsLoaded { get; }
    UiMapConfig? Current { get; }
    string? CurrentPath { get; }
    int EntryCount { get; }
}
