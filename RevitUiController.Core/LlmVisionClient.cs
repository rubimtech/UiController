using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UiController.Core;

public class LlmVisionResult
{
    public bool Found { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public string? Name { get; set; }
    public string? Provider { get; set; }
    public string? Model { get; set; }
    public double? Confidence { get; set; }
}

public class LlmProviderInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAvailable { get; set; }
    public bool SupportsVision { get; set; }
    public string? DefaultModel { get; set; }
}

public static class LlmVisionClient
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private static readonly List<LlmProviderInfo> _providers = new()
    {
        new() { Name = "routerai", DisplayName = "RouterAI", DefaultModel = "qwen/qwen-vl-max", SupportsVision = true },
        new() { Name = "openai", DisplayName = "OpenAI", DefaultModel = "gpt-4o", SupportsVision = true },
        new() { Name = "anthropic", DisplayName = "Anthropic", DefaultModel = "claude-sonnet-4-20250514", SupportsVision = true },
        new() { Name = "ollama", DisplayName = "Ollama (local)", DefaultModel = "llama3.2-vision", SupportsVision = true },
    };

    static LlmVisionClient()
    {
        foreach (var p in _providers)
            p.IsAvailable = p.Name switch
            {
                "routerai" => !string.IsNullOrEmpty(GetEnv("ROUTERAI_API_KEY")),
                "openai" => !string.IsNullOrEmpty(GetEnv("OPENAI_API_KEY")),
                "anthropic" => !string.IsNullOrEmpty(GetEnv("ANTHROPIC_API_KEY")),
                "ollama" => true,
                _ => false
            };
    }

    private static string GetEnv(string name) => Environment.GetEnvironmentVariable(name) ?? "";

    private static string? GetDefaultModel(string providerName)
    {
        return _providers.FirstOrDefault(p => p.Name == providerName)?.DefaultModel;
    }

    public static List<LlmProviderInfo> GetAvailableProviders() => _providers.ToList();

    public static string? ResolveProvider(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            var ordered = _providers.Where(p => p.IsAvailable && p.SupportsVision).ToList();
            return ordered.Count > 0 ? ordered[0].Name : null;
        }
        var match = _providers.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) ||
            p.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase));
        return match?.Name;
    }

    public static async Task<LlmVisionResult?> FindElementAsync(
        string description, string base64Image,
        string? provider = null, string? model = null, int timeoutSec = 30)
    {
        var resolvedProvider = ResolveProvider(provider);
        if (resolvedProvider == null)
            return null;

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSec));

        try
        {
            var logModel = model ?? GetDefaultModel(resolvedProvider);
            LoggingService.Info("LlmVisionClient",
                $"[LLM] Sending screenshot to {resolvedProvider}/{logModel}");

            var (content, usedProvider, usedModel) = resolvedProvider switch
            {
                "routerai" => await CallRouterAIAsync(description, base64Image, model, cts.Token),
                "openai" => await CallOpenAIAsync(description, base64Image, model, cts.Token),
                "anthropic" => await CallAnthropicAsync(description, base64Image, model, cts.Token),
                "ollama" => await CallOllamaAsync(description, base64Image, model, cts.Token),
                _ => (null, null, null)
            };

            if (content == null)
                return null;

            var json = ExtractJson(content);
            if (json == null)
                return null;

            var result = JsonSerializer.Deserialize<LlmVisionRawResult>(json, _jsonOptions);
            if (result == null)
                return null;

            return new LlmVisionResult
            {
                Found = result.Found,
                X = result.X,
                Y = result.Y,
                Name = result.Name,
                Confidence = result.Confidence,
                Provider = usedProvider,
                Model = usedModel
            };
        }
        catch (TaskCanceledException)
        {
            LoggingService.Error("LlmVisionClient", $"[llm] {resolvedProvider}: request timed out after {timeoutSec}s");
            return null;
        }
        catch (Exception ex)
        {
            LoggingService.Error("LlmVisionClient", $"[llm] {resolvedProvider}: {ex.Message}");
            return null;
        }
    }

    private static async Task<(string? content, string provider, string model)> CallRouterAIAsync(
        string description, string base64Image, string? model, CancellationToken ct)
    {
        var apiKey = GetEnv("ROUTERAI_API_KEY");
        var usedModel = model ?? "qwen/qwen-vl-max";
        var body = CreateOpenAIBody(description, base64Image, usedModel);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://routerai.ru/api/v1/chat/completions")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Headers = { Authorization = new("Bearer", apiKey) }
        };
        return (await CallOpenAICompatibleAsync(request, ct), "routerai", usedModel);
    }

    private static async Task<(string? content, string provider, string model)> CallOpenAIAsync(
        string description, string base64Image, string? model, CancellationToken ct)
    {
        var apiKey = GetEnv("OPENAI_API_KEY");
        var usedModel = model ?? "gpt-4o";
        var body = CreateOpenAIBody(description, base64Image, usedModel);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Headers = { Authorization = new("Bearer", apiKey) }
        };
        return (await CallOpenAICompatibleAsync(request, ct), "openai", usedModel);
    }

    private static async Task<string?> CallOpenAICompatibleAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0) return null;
        var message = choices[0].GetProperty("message");
        return message.GetProperty("content").GetString();
    }

    private static string CreateOpenAIBody(string description, string base64Image, string model)
    {
        return JsonSerializer.Serialize(new
        {
            model,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = BuildPrompt(description) },
                        new { type = "image_url", image_url = new { url = $"data:image/png;base64,{base64Image}" } }
                    }
                }
            },
            max_tokens = 500
        }, _jsonOptions);
    }

    private static async Task<(string? content, string provider, string model)> CallAnthropicAsync(
        string description, string base64Image, string? model, CancellationToken ct)
    {
        var apiKey = GetEnv("ANTHROPIC_API_KEY");
        var usedModel = model ?? "claude-sonnet-4-20250514";

        var bodyObj = new
        {
            model = usedModel,
            max_tokens = 500,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = BuildPrompt(description) },
                        new { type = "image", source = new { type = "base64", media_type = "image/png", data = base64Image } }
                    }
                }
            }
        };

        var body = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
            Headers =
            {
                { "x-api-key", apiKey },
                { "anthropic-version", "2023-06-01" }
            }
        };

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content");
        if (content.GetArrayLength() == 0) return (null, "anthropic", usedModel);
        var textContent = content[0].GetProperty("text").GetString();
        return (textContent, "anthropic", usedModel);
    }

    private static async Task<(string? content, string provider, string model)> CallOllamaAsync(
        string description, string base64Image, string? model, CancellationToken ct)
    {
        var usedModel = model ?? "llama3.2-vision";

        var bodyObj = new
        {
            model = usedModel,
            stream = false,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = BuildPrompt(description),
                    images = new[] { base64Image }
                }
            }
        };

        var body = JsonSerializer.Serialize(bodyObj, _jsonOptions);
        var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/chat")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        try
        {
            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var message = doc.RootElement.GetProperty("message");
            return (message.GetProperty("content").GetString(), "ollama", usedModel);
        }
        catch (HttpRequestException ex)
        {
            LoggingService.Error("LlmVisionClient", $"[llm] ollama not available: {ex.Message}");
            return (null, "ollama", usedModel);
        }
    }

    private static string BuildPrompt(string description)
    {
        return $$"""
You are a precise UI automation assistant. Look at this screenshot of Autodesk Revit.

Find the UI element matching this description: "{{description}}"

Return ONLY a JSON object (no markdown, no explanation) with these fields:
{
  "found": true/false,
  "x": <center_x_in_pixels>,
  "y": <center_y_in_pixels>,
  "name": "<detected_element_name_if_available>",
  "confidence": 0.0-1.0
}

If the element is not visible in the screenshot, set "found": false and omit x/y.
The image dimensions are the full screenshot - x,y must be relative to the image.
""";
    }

    private static string? ExtractJson(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('\n');
            if (start > 0)
            {
                var end = trimmed.LastIndexOf("```");
                if (end > start)
                    trimmed = trimmed[(start + 1)..end].Trim();
            }
        }
        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;
        var braceStart = trimmed.IndexOf('{');
        var braceEnd = trimmed.LastIndexOf('}');
        if (braceStart >= 0 && braceEnd > braceStart)
            return trimmed[braceStart..(braceEnd + 1)];
        return null;
    }

    private class LlmVisionRawResult
    {
        public bool Found { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string? Name { get; set; }
        public double? Confidence { get; set; }
    }
}
