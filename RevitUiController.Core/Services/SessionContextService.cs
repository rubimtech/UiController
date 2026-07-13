namespace UiController.Core.Services;

public class SessionContextService : ISessionContextService
{
    public bool IsActive => SessionContext.IsActive;
    public string? Name => SessionContext.Name;
    public string? ActiveDialog => SessionContext.ActiveDialog;
    public string? ActiveTab
    {
        get => SessionContext.ActiveTab;
        set => SessionContext.ActiveTab = value;
    }
    public string? ActiveViewTab
    {
        get => SessionContext.ActiveViewTab;
        set => SessionContext.ActiveViewTab = value;
    }
    public long? ActiveHwnd
    {
        get => SessionContext.ActiveHwnd;
        set => SessionContext.ActiveHwnd = value;
    }
    public int? ActiveMonitor
    {
        get => SessionContext.ActiveMonitor;
        set => SessionContext.ActiveMonitor = value;
    }
    public string? ActiveProcessName
    {
        get => SessionContext.ActiveProcessName;
        set => SessionContext.ActiveProcessName = value;
    }
    public int? ActivePid
    {
        get => SessionContext.ActivePid;
        set => SessionContext.ActivePid = value;
    }
    public string? ActiveMonitorName
    {
        get => SessionContext.ActiveMonitorName;
        set => SessionContext.ActiveMonitorName = value;
    }
    public IReadOnlyList<SessionCommandRecord> CommandHistory => SessionContext.CommandHistory;

    public void Begin(string? name = null) => SessionContext.Begin(name);
    public void End() => SessionContext.End();
    public void PushDialog(string title) => SessionContext.PushDialog(title);
    public void PopDialog() => SessionContext.PopDialog();
    public void SetVariable(string name, object value) => SessionContext.SetVariable(name, value);
    public object? GetVariable(string name) => SessionContext.GetVariable(name);
    public void ResetDialogContext() => SessionContext.ResetDialogContext();
    public object Status() => SessionContext.Status();
    public object FullStatus() => SessionContext.FullStatus();
    public void RecordCommand(string command, object? args, bool success, double durationMs)
        => SessionContext.RecordCommand(command, args, success, durationMs);
}
