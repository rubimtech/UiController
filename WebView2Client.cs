using System.Text.Json;
using Microsoft.Playwright;
using UiController.Core.Models;

namespace RevitUiController;

public class WebView2Client : IAsyncDisposable
{
    private static readonly object RegistryLock = new();

    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private IPage? _page;
    private bool _browserInstalled;

    public bool IsConnected { get; private set; }
    public int Port { get; }
    public int PageCount => _page?.Context?.Pages?.Count ?? 0;
    public string? CurrentUrl => _page?.Url;

    public WebView2Client(int port = 9222)
    {
        Port = port;
    }

    public static void SetupRegistry()
    {
        const string keyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge\WebView2";
        const string valueName = "AdditionalBrowserArguments";
        const string portArg = "--remote-debugging-port=9222";

        lock (RegistryLock)
        {
            var existing = Microsoft.Win32.Registry.GetValue(keyPath, valueName, null) as string;
            if (!string.IsNullOrEmpty(existing) && existing.Contains("--remote-debugging-port"))
            {
                LoggingService.Info("WebView2", $"Registry already has remote debugging: {existing}");
                return;
            }

            var newValue = string.IsNullOrEmpty(existing)
                ? portArg
                : $"{existing.TrimEnd()} {portArg}";

            Microsoft.Win32.Registry.SetValue(keyPath, valueName, newValue, Microsoft.Win32.RegistryValueKind.String);
            LoggingService.Info("WebView2", $"Registry set: {keyPath}\\{valueName} = {newValue}");
        }
    }

    public static bool IsRegistrySetup()
    {
        const string keyPath = @"HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Edge\WebView2";
        const string valueName = "AdditionalBrowserArguments";

        var existing = Microsoft.Win32.Registry.GetValue(keyPath, valueName, null) as string;
        return !string.IsNullOrEmpty(existing) && existing.Contains("--remote-debugging-port");
    }

    public async Task ConnectAsync(int connectTimeoutSec = 30)
    {
        try
        {
            if (!IsRegistrySetup())
            {
                LoggingService.Warn("WebView2", "Registry key for remote debugging port not found. Call SetupRegistry() first.");
            }

            if (!_browserInstalled)
            {
                LoggingService.Info("WebView2", "Installing Playwright Chromium browser...");
                var exitCode = Microsoft.Playwright.Program.Main(["install", "chromium"]);
                _browserInstalled = true;
                LoggingService.Info("WebView2", $"Playwright install exit code: {exitCode}");
            }

            LoggingService.Info("WebView2", $"Connecting to WebView2 CDP on port {Port}...");
            _playwright = await Playwright.CreateAsync();
            _browser = await _playwright.Chromium.ConnectOverCDPAsync(
                $"http://localhost:{Port}",
                new() { Timeout = connectTimeoutSec * 1000 });
            _page = _browser.Contexts[0].Pages[0];
            IsConnected = true;
            LoggingService.Info("WebView2", $"Connected. URL: {CurrentUrl}, Pages: {PageCount}");
        }
        catch (Exception ex)
        {
            LoggingService.Log("ERROR", "WebView2", $"ConnectAsync failed: {ex.Message}");
            await CleanupAsync();
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        await CleanupAsync();
    }

    public async Task ClickAsync(string selector)
    {
        EnsureConnected();
        var resolved = ResolveSelector(selector);
        LoggingService.Info("WebView2", $"Click: {resolved}");
        await _page!.Locator(resolved).ClickAsync();
    }

    public async Task TypeAsync(string selector, string text)
    {
        EnsureConnected();
        var resolved = ResolveSelector(selector);
        LoggingService.Info("WebView2", $"Type '{text}' into: {resolved}");
        await _page!.Locator(resolved).FillAsync(text);
    }

    public async Task<string?> GetTextAsync(string selector)
    {
        EnsureConnected();
        var resolved = ResolveSelector(selector);
        return await _page!.Locator(resolved).InnerTextAsync();
    }

    public async Task<string?> GetAttributeAsync(string selector, string attribute)
    {
        EnsureConnected();
        var resolved = ResolveSelector(selector);
        return await _page!.Locator(resolved).GetAttributeAsync(attribute);
    }

    public async Task WaitForSelectorAsync(string selector, int timeoutSec = 5)
    {
        EnsureConnected();
        var resolved = ResolveSelector(selector);
        await _page!.Locator(resolved).WaitForAsync(new() { Timeout = timeoutSec * 1000 });
    }

    public async Task<string?> GetValueAsync(string selector)
    {
        EnsureConnected();
        var resolved = ResolveSelector(selector);
        var locator = _page!.Locator(resolved);
        var value = await locator.GetAttributeAsync("value");
        if (value == null)
        {
            value = await locator.InputValueAsync();
        }
        return value;
    }

    public async Task SetValueAsync(string selector, string value)
    {
        EnsureConnected();
        var resolved = ResolveSelector(selector);
        await _page!.Locator(resolved).FillAsync(value);
    }

    public async Task<T?> EvaluateAsync<T>(string js)
    {
        EnsureConnected();
        return await _page!.EvaluateAsync<T>(js);
    }

    public async Task<string?> ScreenshotAsync()
    {
        EnsureConnected();
        var bytes = await _page!.ScreenshotAsync(new() { FullPage = true });
        return Convert.ToBase64String(bytes);
    }

    public async Task<List<ElementInfo>> GetSelectorsAsync(string? selector = null)
    {
        EnsureConnected();

        var js = @"() => {
            const elements = document.querySelectorAll('button, a, input, select, textarea');
            const filter = " + (selector != null ? JsonSerializer.Serialize(selector) : "null") + @";
            const results = [];

            function getCssSelector(el) {
                if (el.id) return '#' + CSS.escape(el.id);
                const path = [];
                let current = el;
                while (current && current !== document.body && current !== document.documentElement) {
                    let tag = current.tagName.toLowerCase();
                    if (current.id) {
                        path.unshift('#' + CSS.escape(current.id));
                        break;
                    }
                    let nth = 1;
                    let sibling = current.previousElementSibling;
                    while (sibling) {
                        if (sibling.tagName === current.tagName) nth++;
                        sibling = sibling.previousElementSibling;
                    }
                    tag += ':nth-child(' + nth + ')';
                    path.unshift(tag);
                    current = current.parentElement;
                }
                return path.join(' > ');
            }

            for (const el of elements) {
                const rect = el.getBoundingClientRect();
                const info = {
                    tag: el.tagName.toLowerCase(),
                    text: (el.textContent || '').trim().substring(0, 200),
                    selector: getCssSelector(el),
                    type: el.getAttribute('type'),
                    automationId: el.getAttribute('data-testid') || el.getAttribute('id') || el.getAttribute('name') || '',
                    enabled: !el.disabled,
                    visible: rect.width > 0 && rect.height > 0 && el.offsetParent !== null
                };

                if (filter === null ||
                    info.tag.includes(filter) ||
                    info.text.toLowerCase().includes(filter.toLowerCase()) ||
                    info.selector.includes(filter) ||
                    info.automationId.includes(filter)) {
                    results.push(info);
                }
            }
            return results;
        }";

        var result = await _page!.EvaluateAsync<JsonElement>(js);
        var items = JsonSerializer.Deserialize<List<ElementInfo>>(result.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return items ?? [];
    }

    public async ValueTask DisposeAsync()
    {
        await CleanupAsync();
        GC.SuppressFinalize(this);
    }

    private async Task CleanupAsync()
    {
        try
        {
            IsConnected = false;

            if (_page != null)
            {
                try { await _page.CloseAsync(); } catch { }
                _page = null;
            }

            if (_browser != null)
            {
                try { await _browser.CloseAsync(); } catch { }
                _browser = null;
            }

            _playwright?.Dispose();
            _playwright = null;
        }
        catch (Exception ex)
        {
            LoggingService.Log("ERROR", "WebView2", $"Cleanup error: {ex.Message}");
        }
    }

    private void EnsureConnected()
    {
        if (!IsConnected || _page == null)
            throw new InvalidOperationException("WebView2Client is not connected. Call ConnectAsync first.");
    }

    private static string ResolveSelector(string selector)
    {
        if (string.IsNullOrEmpty(selector))
            throw new ArgumentException("Selector cannot be null or empty", nameof(selector));

        var trimmed = selector.TrimStart();
        if (trimmed.StartsWith("//") || trimmed.StartsWith("(//"))
            return "xpath=" + trimmed;

        return selector;
    }
}
