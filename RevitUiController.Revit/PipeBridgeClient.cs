using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;

namespace RevitUiController.Revit;

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
            var hello = SendCommand("_hello", new { });
            return hello != null;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _heartbeatCts?.Cancel();
        _pipe?.Dispose();
        _eventPipe?.Dispose();
    }

    public string? SendCommand(string method, object? payload = null)
    {
        return null;
    }
}
