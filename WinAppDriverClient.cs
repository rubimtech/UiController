using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace RevitUiController;

public class WinAppDriverClient : IDisposable
{
    private readonly HttpClient _client;
    private string? _sessionId;
    private const string BaseUrl = "http://127.0.0.1:4723";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public bool IsConnected => _sessionId != null;

    public WinAppDriverClient()
    {
        _client = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }

    public bool Connect(IntPtr revitHwnd)
    {
        try
        {
            var caps = new
            {
                desiredCapabilities = new
                {
                    appTopLevelWindow = revitHwnd.ToInt64().ToString("X"),
                    platformName = "Windows",
                    deviceName = "WindowsPC"
                }
            };
            var json = JsonSerializer.Serialize(caps, JsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync($"{BaseUrl}/wd/hub/session", content).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

            if (!response.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(body);
            _sessionId = doc.RootElement.GetProperty("value").GetProperty("sessionId").GetString();
            return _sessionId != null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"WinAppDriver connect failed: {ex.Message}");
            return false;
        }
    }

    public string? FindElement(string usingMethod, string value)
    {
        if (_sessionId == null) return null;
        try
        {
            var payload = new { @using = usingMethod, value };
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync($"{BaseUrl}/wd/hub/session/{_sessionId}/element", content).GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("value").GetProperty("ELEMENT").GetString();
        }
        catch { return null; }
    }

    public bool Click(string elementId)
    {
        if (_sessionId == null) return false;
        try
        {
            var response = _client.PostAsync($"{BaseUrl}/wd/hub/session/{_sessionId}/element/{elementId}/click", null).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public bool TypeText(string elementId, string text)
    {
        if (_sessionId == null) return false;
        try
        {
            var payload = new { text, value = new[] { text } };
            var json = JsonSerializer.Serialize(payload, JsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync($"{BaseUrl}/wd/hub/session/{_sessionId}/element/{elementId}/value", content).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public string? GetSource()
    {
        if (_sessionId == null) return null;
        try
        {
            var response = _client.GetAsync($"{BaseUrl}/wd/hub/session/{_sessionId}/source").GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("value").GetString();
        }
        catch { return null; }
    }

    public void Disconnect()
    {
        if (_sessionId == null) return;
        try { _client.DeleteAsync($"{BaseUrl}/wd/hub/session/{_sessionId}").GetAwaiter().GetResult(); }
        catch { }
        _sessionId = null;
    }

    public void Dispose()
    {
        Disconnect();
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
