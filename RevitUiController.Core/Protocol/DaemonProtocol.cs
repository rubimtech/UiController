using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UiController.Core.Models;

namespace UiController.Core.Protocol;

public class DaemonRequest
{
    public string Command { get; set; } = "";
    public List<string>? Args { get; set; }
    public int? Pid { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
    public bool UseActive { get; set; }
    public int? Timeout { get; set; }
    public int? Interval { get; set; }
    public string? Condition { get; set; }
    public string? SubCommand { get; set; }
    public List<string>? SubArgs { get; set; }
    public string? Action { get; set; }
    public int? Count { get; set; }
    public int? MaxEvents { get; set; }
    public List<DaemonRequest>? Commands { get; set; }
    public string? Type { get; set; }
    public string? Tab { get; set; }
    public string? Panel { get; set; }
    public int? WaitAfter { get; set; }
    public string? Modifiers { get; set; }
    public bool? Retry { get; set; }
    public List<string>? ElementIds { get; set; }
    public string? PropertyName { get; set; }
    public string? Locale { get; set; }
    public string? Strategy { get; set; }
    public string? OnError { get; set; }
    public string? If { get; set; }
    public OnlyIfCondition? OnlyIf { get; set; }
}

public class OnlyIfCondition
{
    public string? Dialog { get; set; }
    public bool? Exists { get; set; }
    public string? Element { get; set; }
    public bool? Enabled { get; set; }
}

public class DaemonResponse
{
    public bool Success { get; set; }
    public ErrorCode ErrorCode { get; set; } = ErrorCode.Unknown;
    public string? Error { get; set; }
    public SelfDescribingError? ErrorInfo { get; set; }
    public object? Data { get; set; }
    public string? Command { get; set; }
    public UiStateDiff? StateDiff { get; set; }
    public string? Screenshot { get; set; }
    public double DurationMs { get; set; }
}

public class DaemonClient : IDisposable
{
    private readonly string _pipeName;
    private System.IO.Pipes.NamedPipeClientStream? _pipe;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public bool IsConnected => _pipe?.IsConnected ?? false;

    public DaemonClient(string pipeName) { _pipeName = pipeName; }

    public bool Connect(int timeoutMs)
    {
        try
        {
            _pipe = new System.IO.Pipes.NamedPipeClientStream(".", _pipeName,
                System.IO.Pipes.PipeDirection.InOut,
                System.IO.Pipes.PipeOptions.Asynchronous);
            _pipe.Connect(timeoutMs);
            _reader = new StreamReader(_pipe, System.Text.Encoding.UTF8);
            _writer = new StreamWriter(_pipe, System.Text.Encoding.UTF8) { AutoFlush = true };
            return true;
        }
        catch { return false; }
    }

    public string? SendRequest(DaemonRequest request)
    {
        if (!IsConnected || _writer == null || _reader == null) return null;
        try
        {
            _writer.WriteLine(JsonSerializer.Serialize(request));
            return _reader.ReadLine();
        }
        catch { return null; }
    }

    public string? SendRaw(string json)
    {
        if (!IsConnected || _writer == null || _reader == null) return null;
        try { _writer.WriteLine(json); return _reader.ReadLine(); }
        catch { return null; }
    }

    public DaemonResponse? SendAndDeserialize(DaemonRequest request)
    {
        var json = SendRequest(request);
        if (json == null) return null;
        try { return JsonSerializer.Deserialize<DaemonResponse>(json, Options); }
        catch { return null; }
    }

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Dispose() { _pipe?.Dispose(); }
}
