using FlaUI.Core.AutomationElements;
using static RevitUiController.Core.AutomationEventService;

namespace RevitUiController.Core.Services;

public class EventServiceWrapper : IEventService
{
    private AutomationEventService? _inner;

    public bool IsListening => _inner?.IsListening ?? false;

    public event EventHandler<UiEventRecord>? OnEvent
    {
        add { if (_inner != null) _inner.OnEvent += value; }
        remove { if (_inner != null) _inner.OnEvent -= value; }
    }

    public void Initialize(AutomationEventService eventService)
    {
        _inner = eventService;
    }

    public void StartListening() => _inner?.StartListening();
    public void StopListening() => _inner?.StopListening();
    public IEnumerable<UiEventRecord> GetRecentEvents(int count = 20) => _inner?.GetRecentEvents(count) ?? [];
    public void ClearLog() => _inner?.ClearLog();
    public Task<AutomationElement?> WaitForDialogAsync(string title, int timeoutMs, CancellationToken ct = default)
        => _inner?.WaitForDialogAsync(title, timeoutMs, ct) ?? Task.FromResult<AutomationElement?>(null);
    public Task<bool> WaitForDialogCloseAsync(string title, int timeoutMs, CancellationToken ct = default)
        => _inner?.WaitForDialogCloseAsync(title, timeoutMs, ct) ?? Task.FromResult(false);
    public Task<AutomationElement?> WaitForElementAsync(string name, int timeoutMs, CancellationToken ct = default)
        => _inner?.WaitForElementAsync(name, timeoutMs, ct) ?? Task.FromResult<AutomationElement?>(null);
    public Task<bool> WaitForProgressAsync(int timeoutMs, CancellationToken ct = default)
        => _inner?.WaitForProgressAsync(timeoutMs, ct) ?? Task.FromResult(false);

    public void Dispose()
    {
        _inner?.Dispose();
    }
}
