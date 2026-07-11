namespace UiController.Core.Services;

public interface ISessionContextService
{
    bool IsActive { get; }
    string? ActiveDialog { get; }
    string? ActiveTab { get; set; }
    string? ActiveViewTab { get; set; }
    long? ActiveHwnd { get; set; }
    int? ActiveMonitor { get; set; }
    string? ActiveProcessName { get; set; }
    int? ActivePid { get; set; }
    string? ActiveMonitorName { get; set; }

    void Begin();
    void End();
    void PushDialog(string title);
    void PopDialog();
    void SetVariable(string name, object value);
    object? GetVariable(string name);
    void ResetDialogContext();
    object Status();
}
