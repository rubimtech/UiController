namespace UiController.Core.Services;

public class UiMapService : IUiMapService
{
    public bool IsLoaded => UiMap.IsLoaded;
    public UiMapConfig? Current => UiMap.Current;
    public string? CurrentPath => UiMap.CurrentPath;
    public int EntryCount => UiMap.EntryCount;

    public bool Load(string path) => UiMap.Load(path);
    public bool TryLoadDefault() => UiMap.TryLoadDefault();
    public void Save(string path) => UiMap.Save(path);
    public List<SelectorCandidate> Resolve(string logicalName, int? revitYear = null) => UiMap.Resolve(logicalName, revitYear);
    public UiMapEntry? GetEntry(string logicalName) => UiMap.GetEntry(logicalName);
    public void Register(string logicalName, UiMapEntry entry) => UiMap.Register(logicalName, entry);
    public Dictionary<string, UiMapEntry> GetAllEntries() => UiMap.GetAllEntries();
    public Dictionary<string, UiMapEntry> FindEntries(string? filter = null) => UiMap.FindEntries(filter);
    public string? ResolveLocale(string name) => UiMap.ResolveLocale(name);
    public void Unload() => UiMap.Unload();
}
