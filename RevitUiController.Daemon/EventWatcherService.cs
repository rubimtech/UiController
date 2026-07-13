using System.Collections.Concurrent;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using UiController.Core;

namespace UiController.Daemon;

public class EventWatcherService : IDisposable
{
    private readonly ConcurrentQueue<UiEvent> _eventQueue = new();
    private CancellationTokenSource? _cts;
    private WindowSession? _session;
    private Task? _pollTask;

    public int Count => _eventQueue.Count;

    public void Start(WindowSession session)
    {
        Stop();
        _session = session;
        _cts = new CancellationTokenSource();
        _pollTask = PollLoopAsync(_cts.Token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts = null;
        _pollTask = null;
    }

    public bool TryDequeue(out UiEvent evt) => _eventQueue.TryDequeue(out evt);

    public void ScanDialogs(IEnumerable<AutomationElement> dialogs)
    {
        foreach (var d in dialogs)
        {
            if (d == null) continue;
            var title = d.Name ?? "";
            if (string.IsNullOrEmpty(title)) continue;

            var text = "";
            try
            {
                var children = AutomationHelper.SafeGetChildren(d, 2000);
                foreach (var c in children)
                {
                    if (c.Name == title) continue;
                    if (!string.IsNullOrEmpty(c.Name))
                        text += c.Name + " ";
                }
            }
            catch { }

            _eventQueue.Enqueue(new UiEvent
            {
                Type = "dialog",
                Title = title,
                Text = text.Trim(),
                Timestamp = DateTime.UtcNow
            });
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        var lastDialogSnapshot = new HashSet<string>();

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(300, ct);

                if (_session?.MainWindow == null) continue;

                var dialogs = AutomationHelper.FindActiveDialogs(_session.MainWindow);
                var currentTitles = dialogs
                    .Where(d => d != null && !string.IsNullOrEmpty(d.Name))
                    .Select(d => d.Name!)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var newDialogs = currentTitles.Except(lastDialogSnapshot, StringComparer.OrdinalIgnoreCase).ToList();
                var closedDialogs = lastDialogSnapshot.Except(currentTitles, StringComparer.OrdinalIgnoreCase).ToList();

                foreach (var title in newDialogs)
                {
                    var dialog = dialogs.FirstOrDefault(d => d.Name == title);
                    var text = "";
                    if (dialog != null)
                    {
                        try
                        {
                            var children = AutomationHelper.SafeGetChildren(dialog, 2000);
                            foreach (var c in children)
                            {
                                if (!string.IsNullOrEmpty(c.Name) && c.Name != title)
                                    text += c.Name + " ";
                            }
                        }
                        catch { }
                    }

                    _eventQueue.Enqueue(new UiEvent
                    {
                        Type = "dialog-opened",
                        Title = title,
                        Text = text.Trim(),
                        Timestamp = DateTime.UtcNow
                    });
                }

                foreach (var title in closedDialogs)
                {
                    _eventQueue.Enqueue(new UiEvent
                    {
                        Type = "dialog-closed",
                        Title = title,
                        Timestamp = DateTime.UtcNow
                    });
                }

                lastDialogSnapshot = currentTitles;

                if (currentTitles.Count > 0)
                {
                    var topDialog = dialogs.FirstOrDefault();
                    if (topDialog != null)
                    {
                        try
                        {
                            var children = AutomationHelper.SafeGetChildren(topDialog, 2000);
                            foreach (var c in children)
                            {
                                if (c.ControlType == ControlType.StatusBar && !string.IsNullOrEmpty(c.Name))
                                {
                                    _eventQueue.Enqueue(new UiEvent
                                    {
                                        Type = "status",
                                        Title = "StatusBar",
                                        Text = c.Name,
                                        Timestamp = DateTime.UtcNow
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch { }
        }
    }

    public void Dispose() { Stop(); }
}

public class UiEvent
{
    public string Type { get; set; } = "";
    public string? Title { get; set; }
    public string? Text { get; set; }
    public DateTime Timestamp { get; set; }
}
