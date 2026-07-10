using System.IO.Pipes;
using System.Text;

namespace RevitUiController;

public class PipeBridgeClient : IDisposable
{
    private NamedPipeClientStream? _pipe;
    private Thread? _heartbeatThread;
    private volatile bool _running;
    private readonly object _lock = new();

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
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Pipe connect failed: {ex.Message}");
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

        return Encoding.UTF8.GetString(dataBuf);
    }

    private void StartHeartbeat()
    {
        _running = true;
        _heartbeatThread = new Thread(() =>
        {
            while (_running)
            {
                Thread.Sleep(10000);
                try { SendCommand("_heartbeat", new { pid = Environment.ProcessId }, 2000); }
                catch (Exception ex) { LoggingService.Warn("Safe", $"PipeBridge heartbeat: {ex.Message}"); break; }
            }
        });
        _heartbeatThread.IsBackground = true;
        _heartbeatThread.Start();
    }

    public void Disconnect()
    {
        _running = false;
        try { SendCommand("_client_shutdown", new { }, 1000); } catch (Exception ex) { LoggingService.Warn("Safe", $"PipeBridge disconnect: {ex.Message}"); }
        lock (_lock)
        {
            _pipe?.Dispose();
            _pipe = null;
        }
    }

    public void Dispose()
    {
        Disconnect();
        GC.SuppressFinalize(this);
    }
}
