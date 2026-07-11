using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace UiController.Core;

public class WinAppDriverProvider : IAutomationProvider
{
    private readonly WinAppDriverClient _client;

    public WinAppDriverProvider(WinAppDriverClient client)
    {
        _client = client;
    }

    public WinAppDriverClient Client => _client;
    public UIA3Automation? UIA3 => null;
    public bool IsUia3 => false;
    public string Name => "WinAppDriver";

    public AutomationElement GetDesktop() => throw new NotSupportedException($"{nameof(WinAppDriverProvider)} does not support GetDesktop");
    public AutomationElement GetRootElement(IntPtr hwnd) => throw new NotSupportedException($"{nameof(WinAppDriverProvider)} does not support GetRootElement");
    public AutomationElement? FindFirst(AutomationElement root, string name, int timeoutMs = 3000) => throw new NotSupportedException($"{nameof(WinAppDriverProvider)} element finding not yet implemented");
    public AutomationElement? FindFirstEnabledVisible(AutomationElement root, string name) => throw new NotSupportedException($"{nameof(WinAppDriverProvider)} element finding not yet implemented");
    public AutomationElement[] FindAllChildren(AutomationElement element) => throw new NotSupportedException($"{nameof(WinAppDriverProvider)} does not support FindAllChildren");
    public AutomationElement[] FindAllByControlType(AutomationElement root, ControlType controlType) => throw new NotSupportedException($"{nameof(WinAppDriverProvider)} does not support FindAllByControlType");
    public AutomationElement[] FindActiveDialogs(AutomationElement root) => throw new NotSupportedException($"{nameof(WinAppDriverProvider)} does not support FindActiveDialogs");

    public void Dispose()
    {
    }
}
