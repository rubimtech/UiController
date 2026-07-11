using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace UiController.Core;

public class UIA3AutomationProvider : IAutomationProvider
{
    private readonly UIA3Automation _automation;

    public UIA3AutomationProvider()
    {
        _automation = new UIA3Automation();
    }

    public UIA3Automation? UIA3 => _automation;
    public bool IsUia3 => true;
    public string Name => "UIA3";

    public AutomationElement GetDesktop()
    {
        return _automation.GetDesktop();
    }

    public AutomationElement GetRootElement(IntPtr hwnd)
    {
        return _automation.FromHandle(hwnd);
    }

    public AutomationElement? FindFirst(AutomationElement root, string name, int timeoutMs = 3000)
    {
        var results = AutomationHelper.FindControlsByName(root, name, 1);
        return results.Count > 0 ? results[0] : null;
    }

    public AutomationElement? FindFirstEnabledVisible(AutomationElement root, string name)
    {
        return AutomationHelper.FindFirstEnabledVisible(root, name);
    }

    public AutomationElement[] FindAllChildren(AutomationElement element)
    {
        return AutomationHelper.SafeGetChildren(element);
    }

    public AutomationElement[] FindAllByControlType(AutomationElement root, ControlType controlType)
    {
        var result = AutomationHelper.FindFirstChildByType(root, controlType);
        return result != null ? new[] { result } : Array.Empty<AutomationElement>();
    }

    public AutomationElement[] FindActiveDialogs(AutomationElement root)
    {
        return AutomationHelper.FindActiveDialogs(root).ToArray();
    }

    public void Dispose()
    {
        _automation?.Dispose();
    }
}
