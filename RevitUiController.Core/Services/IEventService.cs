using FlaUI.Core.AutomationElements;
using static RevitUiController.Core.AutomationEventService;

namespace RevitUiController.Core.Services;

public interface IEventService : IDisposable
{
    void StartListening();
    void StopListening();
    IEnumerable<UiEventRecord> GetRecentEvents(int count = 20);
    void ClearLog();
    Task<AutomationElement?> WaitForDialogAsync(string title, int timeoutMs, CancellationToken ct = default);
    Task<bool> WaitForDialogCloseAsync(string title, int timeoutMs, CancellationToken ct = default);
    Task<AutomationElement?> WaitForElementAsync(string name, int timeoutMs, CancellationToken ct = default);
    Task<bool> WaitForProgressAsync(int timeoutMs, CancellationToken ct = default);
    bool IsListening { get; }
    event EventHandler<UiEventRecord>? OnEvent;
}
