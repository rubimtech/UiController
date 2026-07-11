namespace RevitUiController.Core.Services;

public class LlmVisionService : ILlmVisionService
{
    public List<LlmProviderInfo> GetAvailableProviders() => LlmVisionClient.GetAvailableProviders();
    public string? ResolveProvider(string? name) => LlmVisionClient.ResolveProvider(name);

    public Task<LlmVisionResult?> FindElementAsync(
        string description, string base64Image,
        string? provider = null, string? model = null, int timeoutSec = 30)
        => LlmVisionClient.FindElementAsync(description, base64Image, provider, model, timeoutSec);
}
