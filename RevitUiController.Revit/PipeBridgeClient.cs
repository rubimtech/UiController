using System.Collections.Concurrent;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace RevitUiController.Revit;

public class PipeBridgeClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private readonly object _lock = new();
    private NamedPipeClientStream? _eventPipe;
    private StreamReader? _eventReader;

    public record PipeEvent(string Type, string? Data, DateTime Timestamp);
    public event Action<PipeEvent>? OnEvent;
    public ConcurrentQueue<PipeEvent> EventQueue { get; } = new();

    public bool IsConnected => _pipe?.IsConnected ?? false;

    public bool Connect(int timeoutMs = 5000)
    {
        try
        {
            _pipe = new NamedPipeClientStream(".", "ReVibe", PipeDirection.InOut, PipeOptions.Asynchronous);
            _pipe.Connect(timeoutMs);
            _reader = new StreamReader(_pipe, Encoding.UTF8);
            _writer = new StreamWriter(_pipe, Encoding.UTF8) { AutoFlush = true };

            var hello = SendCommand("_hello", new { });
            if (hello == null) return false;

            StartHeartbeat();
            TryConnectEventPipe();
            return true;
        }
        catch (Exception ex)
        {
            UiController.Core.LoggingService.Error("PipeBridge", $"Connect failed: {ex.Message}");
            return false;
        }
    }

    private void TryConnectEventPipe()
    {
        try
        {
            _eventPipe = new NamedPipeClientStream(".", "ReVibe.Events", PipeDirection.In, PipeOptions.Asynchronous);
            _eventPipe.Connect(1000);
            _eventReader = new StreamReader(_eventPipe, Encoding.UTF8);

            _ = Task.Run(async () =>
            {
                try
                {
                    while (_eventPipe.IsConnected)
                    {
                        var line = await _eventReader.ReadLineAsync();
                        if (line == null) break;

                        try
                        {
                            var doc = JsonDocument.Parse(line);
                            var type = doc.RootElement.TryGetProperty("type", out var t) ? t.GetString() : "unknown";
                            var data = doc.RootElement.TryGetProperty("data", out var d) ? d.GetRawText() : null;
                            var evt = new PipeEvent(type ?? "unknown", data, DateTime.UtcNow);
                            EventQueue.Enqueue(evt);
                            OnEvent?.Invoke(evt);
                        }
                        catch { }
                    }
                }
                catch { }
            });
        }
        catch
        {
            // Event pipe is optional
        }
    }

    private void StartHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = Task.Run(async () =>
        {
            while (!_heartbeatCts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(5000, _heartbeatCts.Token);
                    SendCommand("_heartbeat", new { timestamp = DateTime.UtcNow });
                }
                catch (OperationCanceledException) { break; }
                catch
                {
                    // Connection may be lost
                    break;
                }
            }
        });
    }

    public void Dispose()
    {
        _heartbeatCts?.Cancel();
        _pipe?.Dispose();
        _eventPipe?.Dispose();
    }

    public string? SendCommand(string method, object? payload = null)
    {
        lock (_lock)
        {
            if (_pipe == null || !_pipe.IsConnected || _writer == null || _reader == null)
                return null;

            try
            {
                var request = JsonSerializer.Serialize(new
                {
                    method,
                    payload,
                    id = Guid.NewGuid().ToString("N")[..8]
                });

                _writer.WriteLine(request);

                var response = _reader.ReadLine();
                return response;
            }
            catch (Exception ex)
            {
                UiController.Core.LoggingService.Error("PipeBridge", $"SendCommand({method}) failed: {ex.Message}");
                return null;
            }
        }
    }

    public T? SendCommand<T>(string method, object? payload = null)
    {
        var json = SendCommand(method, payload);
        if (json == null) return default;
        try
        {
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("result", out var result))
                return JsonSerializer.Deserialize<T>(result.GetRawText());
            if (doc.RootElement.TryGetProperty("data", out var data))
                return JsonSerializer.Deserialize<T>(data.GetRawText());
            return JsonSerializer.Deserialize<T>(json);
        }
        catch { return default; }
    }

    public UndoStackInfo? QueryUndoStack()
    {
        return SendCommand<UndoStackInfo>("undo_stack", new { });
    }

    public bool PerformUndo(int count = 1)
    {
        var result = SendCommand("undo", new { count });
        return result != null;
    }
}

public class UndoStackInfo
{
    public int UndoCount { get; set; }
    public int RedoCount { get; set; }
    public List<string>? UndoItems { get; set; }
    public List<string>? RedoItems { get; set; }
    public bool CanUndo => UndoCount > 0;
}
