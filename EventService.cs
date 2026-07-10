using System.Collections.Concurrent;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.Core.EventHandlers;
using FlaUI.Core.Identifiers;
using FlaUI.UIA3;

namespace RevitUiController;

public class AutomationEventService : IDisposable
{
    private readonly UIA3Automation _automation;
    private readonly AutomationElement _desktop;
    private readonly AutomationElement _rootWindow;
    private readonly ConcurrentQueue<UiEventRecord> _eventLog = new();
    private readonly List<IDisposable> _handlers = new();
    private bool _listening;

    public record UiEventRecord(string Type, string Name, string AutomationId, string ControlType, int[]? RuntimeId, DateTime Timestamp);

    public AutomationEventService(UIA3Automation automation, AutomationElement rootWindow)
    {
        _automation = automation;
        _rootWindow = rootWindow;
        _desktop = automation.GetDesktop();
    }

    public bool IsListening => _listening;

    public void StartListening()
    {
        if (_listening) return;
        _listening = true;

        var h1 = _automation.RegisterFocusChangedEvent(OnFocusChanged);
        _handlers.Add(h1);

        var h2 = _desktop.RegisterStructureChangedEvent(TreeScope.Subtree, OnStructureChanged);
        _handlers.Add(h2);

        var windowEventIds = _automation.EventLibrary.Window;
        var h3 = _desktop.RegisterAutomationEvent(windowEventIds.WindowOpenedEvent, TreeScope.Subtree, OnWindowEvent);
        _handlers.Add(h3);

        var h4 = _desktop.RegisterAutomationEvent(windowEventIds.WindowClosedEvent, TreeScope.Subtree, OnWindowEvent);
        _handlers.Add(h4);

        LogEventString("Service", "ListenerStarted");
    }

    public void StopListening()
    {
        if (!_listening) return;
        _listening = false;

        foreach (var h in _handlers)
        {
            try { h.Dispose(); } catch { }
        }
        _handlers.Clear();

        LogEventString("Service", "ListenerStopped");
    }

    public IEnumerable<UiEventRecord> GetRecentEvents(int count = 20) =>
        _eventLog.Reverse().Take(count);

    public void ClearLog() => _eventLog.Clear();

    public async Task<AutomationElement?> WaitForDialogAsync(string title, int timeoutMs, CancellationToken ct = default)
    {
        var found = FindDialogByTitle(title);
        if (found != null) return found;

        var tcs = new TaskCompletionSource<AutomationElement?>();
        using var ctr = ct.Register(() => tcs.TrySetResult(null));

        EventHandler<UiEventRecord> handler = null!;
        handler = (_, record) =>
        {
            if (record.Type == "WindowOpened" &&
                record.Name.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                tcs.TrySetResult(FindDialogByTitle(title));
                OnEvent -= handler;
            }
        };
        OnEvent += handler;

        _ = Task.Delay(timeoutMs, ct).ContinueWith(_ => tcs.TrySetResult(null), TaskContinuationOptions.NotOnCanceled);

        return await tcs.Task;
    }

    public async Task<bool> WaitForDialogCloseAsync(string title, int timeoutMs, CancellationToken ct = default)
    {
        if (FindDialogByTitle(title) == null) return true;

        var tcs = new TaskCompletionSource<bool>();
        using var ctr = ct.Register(() => tcs.TrySetResult(false));

        EventHandler<UiEventRecord> handler = null!;
        handler = (_, record) =>
        {
            if (record.Type == "WindowClosed" &&
                record.Name.Contains(title, StringComparison.OrdinalIgnoreCase))
            {
                tcs.TrySetResult(true);
                OnEvent -= handler;
            }
        };
        OnEvent += handler;

        _ = Task.Delay(timeoutMs, ct).ContinueWith(_ => tcs.TrySetResult(false), TaskContinuationOptions.NotOnCanceled);

        return await tcs.Task;
    }

    public async Task<AutomationElement?> WaitForElementAsync(string name, int timeoutMs, CancellationToken ct = default)
    {
        var found = AutomationHelper.FindFirstEnabledVisible(_rootWindow, name);
        if (found != null) return found;

        var tcs = new TaskCompletionSource<AutomationElement?>();
        using var ctr = ct.Register(() => tcs.TrySetResult(null));

        EventHandler<UiEventRecord> handler = null!;
        handler = (_, record) =>
        {
            if (record.Type == "StructureChanged_Added" &&
                (record.Name.Contains(name, StringComparison.OrdinalIgnoreCase) ||
                 record.AutomationId.Contains(name, StringComparison.OrdinalIgnoreCase)))
            {
                var el = AutomationHelper.FindFirstEnabledVisible(_rootWindow, name);
                if (el != null)
                {
                    tcs.TrySetResult(el);
                    OnEvent -= handler;
                }
            }
        };
        OnEvent += handler;

        _ = Task.Delay(timeoutMs, ct).ContinueWith(_ => tcs.TrySetResult(null), TaskContinuationOptions.NotOnCanceled);

        return await tcs.Task;
    }

    public async Task<bool> WaitForProgressAsync(int timeoutMs, CancellationToken ct = default)
    {
        var tcs = new TaskCompletionSource<bool>();
        using var ctr = ct.Register(() => tcs.TrySetResult(false));

        EventHandler<UiEventRecord> handler = null!;
        handler = (_, record) =>
        {
            if (record.Type == "StructureChanged_Removed")
            {
                if (!HasProgressBar(_rootWindow))
                {
                    tcs.TrySetResult(true);
                    OnEvent -= handler;
                }
            }
        };
        OnEvent += handler;

        _ = Task.Delay(timeoutMs, ct).ContinueWith(_ => tcs.TrySetResult(false), TaskContinuationOptions.NotOnCanceled);

        return await tcs.Task;
    }

    public event EventHandler<UiEventRecord>? OnEvent;

    private AutomationElement? FindDialogByTitle(string title)
    {
        foreach (var c in AutomationHelper.SafeGetChildren(_rootWindow, 5000))
        {
            try
            {
                if (c.ControlType == ControlType.Window &&
                    (c.Name ?? "").Contains(title, StringComparison.OrdinalIgnoreCase))
                    return c;
            }
            catch { }
        }
        return null;
    }

    private static bool HasProgressBar(AutomationElement root)
    {
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(root);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    if (el.ControlType == ControlType.ProgressBar) return true;
                    foreach (var c in AutomationHelper.SafeGetChildren(el, 2000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }
        return false;
    }

    private void OnFocusChanged(AutomationElement element)
    {
        LogEvent("FocusChanged", element);
    }

    private void OnStructureChanged(AutomationElement element, StructureChangeType changeType, int[] runtimeId)
    {
        var type = changeType switch
        {
            StructureChangeType.ChildAdded => "StructureChanged_Added",
            StructureChangeType.ChildRemoved => "StructureChanged_Removed",
            StructureChangeType.ChildrenBulkAdded => "StructureChanged_BulkAdded",
            StructureChangeType.ChildrenBulkRemoved => "StructureChanged_BulkRemoved",
            StructureChangeType.ChildrenInvalidated => "StructureChanged_Invalidated",
            _ => "StructureChanged_Unknown"
        };
        LogEvent(type, element, runtimeId);
    }

    private void OnWindowEvent(AutomationElement element, EventId eventId)
    {
        var type = eventId == _automation.EventLibrary.Window.WindowOpenedEvent
            ? "WindowOpened"
            : "WindowClosed";
        LogEvent(type, element);
    }

    private void LogEventString(string type, string message)
    {
        try
        {
            var record = new UiEventRecord(type, message, "", "Service", null, DateTime.UtcNow);
            _eventLog.Enqueue(record);
            OnEvent?.Invoke(this, record);
            while (_eventLog.Count > 1000)
                _eventLog.TryDequeue(out _);
        }
        catch { }
    }

    private void LogEvent(string type, AutomationElement element, int[]? runtimeId = null)
    {
        try
        {
            var name = element.Name ?? "";
            var autoId = element.AutomationId ?? "";
            var ctrlType = element.ControlType.ToString();
            var record = new UiEventRecord(type, name, autoId, ctrlType, runtimeId, DateTime.UtcNow);
            _eventLog.Enqueue(record);
            OnEvent?.Invoke(this, record);
            while (_eventLog.Count > 1000)
                _eventLog.TryDequeue(out _);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        StopListening();
    }
}
