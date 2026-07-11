namespace RevitUiController.Core.Services;

public interface ILlmVisionService
{
    List<LlmProviderInfo> GetAvailableProviders();
    string? ResolveProvider(string? name);
    Task<LlmVisionResult?> FindElementAsync(
        string description, string base64Image,
        string? provider = null, string? model = null, int timeoutSec = 30);
}
