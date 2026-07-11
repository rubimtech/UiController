namespace UiController.Core.Services;

public class SessionContextService : ISessionContextService
{
    public bool IsActive => SessionContext.IsActive;
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

    public void Begin() => SessionContext.Begin();
    public void End() => SessionContext.End();
    public void PushDialog(string title) => SessionContext.PushDialog(title);
    public void PopDialog() => SessionContext.PopDialog();
    public void SetVariable(string name, object value) => SessionContext.SetVariable(name, value);
    public object? GetVariable(string name) => SessionContext.GetVariable(name);
    public void ResetDialogContext() => SessionContext.ResetDialogContext();
    public object Status() => SessionContext.Status();
}
