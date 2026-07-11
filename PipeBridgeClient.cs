using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace RevitUiController;

public class PipeBridgeClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private readonly object _lock = new();
    private NamedPipeClientStream? _eventPipe;

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
            var hello = SendCommand("_hello", new
            {
                clientType = "ext",
                processId = Environment.ProcessId,
                loginUser = "uictrl"
            });
            StartHeartbeat();
            StartEventListener();
            return true;
        }
        catch (Exception ex)
        {
            LoggingService.Error("PipeBridgeClient", $"Pipe connect failed: {ex.Message}");
            return false;
        }
    }

    public string? SendCommand(string command, object payload, int timeoutMs = 30000)
    {
        if (_pipe == null || !_pipe.IsConnected) return null;

        var correlationId = Guid.NewGuid().ToString();
        var msg = new
        {
            command,
            payload,
            correlationId,
            timestamp = DateTime.UtcNow.ToString("O")
        };
        var json = System.Text.Json.JsonSerializer.Serialize(msg);
        var bytes = Encoding.UTF8.GetBytes(json);

        lock (_lock)
        {
            _pipe.Write(BitConverter.GetBytes(bytes.Length), 0, 4);
            _pipe.Write(bytes, 0, bytes.Length);
            _pipe.Flush();
        }

        return ReadResponse(timeoutMs);
    }

    private string? ReadResponse(int timeoutMs)
    {
        if (_pipe == null) return null;
        var lenBuf = new byte[4];
        int read = 0;
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (read < 4)
        {
            if (DateTime.UtcNow > deadline) return null;
            try
            {
                var n = _pipe.Read(lenBuf, read, 4 - read);
                if (n == 0) return null;
                read += n;
            }
            catch (Exception ex) { LoggingService.Warn("Safe", $"PipeBridge len read: {ex.Message}"); return null; }
        }

        var len = BitConverter.ToInt32(lenBuf, 0);
        if (len <= 0 || len > 4 * 1024 * 1024) return null;

        var dataBuf = new byte[len];
        read = 0;
        while (read < len)
        {
            if (DateTime.UtcNow > deadline) return null;
            try
            {
                var n = _pipe.Read(dataBuf, read, len - read);
                if (n == 0) return null;
                read += n;
            }
            catch (Exception ex) { LoggingService.Warn("Safe", $"PipeBridge data read: {ex.Message}"); return null; }
        }

        var text = Encoding.UTF8.GetString(dataBuf);
        ProcessIncomingMessage(text);
        return text;
    }

    private void ProcessIncomingMessage(string json)
    {
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "event")
            {
                var eventType = root.TryGetProperty("eventType", out var et) ? et.GetString() ?? "unknown" : "unknown";
                var data = root.TryGetProperty("data", out var d) ? d.GetRawText() : null;
                var pipeEvent = new PipeEvent(eventType, data, DateTime.UtcNow);
                EventQueue.Enqueue(pipeEvent);
                while (EventQueue.Count > 1000)
                    EventQueue.TryDequeue(out _);
                OnEvent?.Invoke(pipeEvent);
            }
        }
        catch { }
    }

    private void StartEventListener()
    {
        try
        {
            _eventPipe = new NamedPipeClientStream(".", "ReVibe", PipeDirection.InOut, PipeOptions.Asynchronous);
            _eventPipe.Connect(2000);
            var hello = SendCommand("_hello", new { clientType = "ext_events", processId = Environment.ProcessId, loginUser = "uictrl" });
            SendCommand("_subscribe_events", new { });
            _ = EventListenerLoopAsync();
        }
        catch (Exception ex)
        {
            LoggingService.Warn("Safe", $"PipeBridge event listener start: {ex.Message}");
        }
    }

    private async Task EventListenerLoopAsync()
    {
        if (_eventPipe == null) return;
        var buf = new byte[4];
        try
        {
            while (_eventPipe.IsConnected)
            {
                int read = 0;
                while (read < 4)
                {
                    var n = await _eventPipe.ReadAsync(buf, read, 4 - read);
                    if (n == 0) return;
                    read += n;
                }
                var len = BitConverter.ToInt32(buf, 0);
                if (len <= 0 || len > 4 * 1024 * 1024) continue;
                var dataBuf = new byte[len];
                read = 0;
                while (read < len)
                {
                    var n = await _eventPipe.ReadAsync(dataBuf, read, len - read);
                    if (n == 0) return;
                    read += n;
                }
                var json = Encoding.UTF8.GetString(dataBuf);
                ProcessIncomingMessage(json);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { LoggingService.Warn("Safe", $"PipeBridge event loop: {ex.Message}"); }
    }

    private void StartHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = HeartbeatLoopAsync(_heartbeatCts.Token);
    }

    private async Task HeartbeatLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                try { SendCommand("_heartbeat", new { pid = Environment.ProcessId }, 2000); }
                catch (Exception ex) { LoggingService.Warn("Safe", $"PipeBridge heartbeat: {ex.Message}"); break; }
            }
        }
        catch (OperationCanceledException) { }
    }

    public void Disconnect()
    {
        _heartbeatCts?.Cancel();
        try { SendCommand("_client_shutdown", new { }, 1000); } catch (Exception ex) { LoggingService.Warn("Safe", $"PipeBridge disconnect: {ex.Message}"); }
        lock (_lock)
        {
            _pipe?.Dispose();
            _pipe = null;
            _eventPipe?.Dispose();
            _eventPipe = null;
        }
        while (EventQueue.Count > 1000)
            EventQueue.TryDequeue(out _);
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
