using System.Net.Http;
using System.Text;
using System.Text.Json;
using FlaUI.UIA3;

namespace RevitUiController.Core;

public class WinAppDriverClient : IDisposable
{
    private readonly HttpClient _client;
    private string? _sessionId;
    private const string BaseUrl = "http://127.0.0.1:4723";
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static WinAppDriverClient? Current { get; set; }
    public static UIA3Automation? CurrentAutomation { get; set; }

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
        catch
        {
            return false;
        }
    }

    public string? CaptureScreenshot()
    {
        if (_sessionId == null) return null;
        try
        {
            var response = _client.GetAsync($"{BaseUrl}/wd/hub/session/{_sessionId}/screenshot").GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode) return null;
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("value").GetString();
        }
        catch { return null; }
    }

    public bool ClickAt(int x, int y)
    {
        if (_sessionId == null) return false;
        try
        {
            var actions = new[]
            {
                new
                {
                    type = "pointer",
                    id = "mouse",
                    parameters = new { pointerType = "mouse" },
                    actions = new object[]
                    {
                        new { type = "pointerMove", x, y, origin = "viewport" },
                        new { type = "pointerDown", button = 0 },
                        new { type = "pointerUp", button = 0 }
                    }
                }
            };
            var json = JsonSerializer.Serialize(actions, JsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync($"{BaseUrl}/wd/hub/session/{_sessionId}/actions", content).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public bool Drag(int x1, int y1, int x2, int y2, int steps = 10)
    {
        if (_sessionId == null) return false;
        try
        {
            var actionsList = new List<object>
            {
                new { type = "pointerMove", x = x1, y = y1, origin = "viewport" },
                new { type = "pointerDown", button = 0 },
                new { type = "pause", duration = 50 }
            };

            for (int i = 1; i <= steps; i++)
            {
                var x = x1 + (x2 - x1) * i / steps;
                var y = y1 + (y2 - y1) * i / steps;
                actionsList.Add(new { type = "pointerMove", x, y, origin = "viewport", duration = 20 });
            }

            actionsList.Add(new { type = "pointerUp", button = 0 });

            var actions = new[]
            {
                new
                {
                    type = "pointer",
                    id = "mouse",
                    parameters = new { pointerType = "mouse" },
                    actions = actionsList
                }
            };

            var json = JsonSerializer.Serialize(actions, JsonOpts);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = _client.PostAsync($"{BaseUrl}/wd/hub/session/{_sessionId}/actions", content).GetAwaiter().GetResult();
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
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
