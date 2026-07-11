using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace UiController.Core;

public interface IAutomationProvider : IDisposable
{
    AutomationElement GetDesktop();
    AutomationElement GetRootElement(IntPtr hwnd);
    AutomationElement? FindFirst(AutomationElement root, string name, int timeoutMs = 3000);
    AutomationElement? FindFirstEnabledVisible(AutomationElement root, string name);
    AutomationElement[] FindAllChildren(AutomationElement element);
    AutomationElement[] FindAllByControlType(AutomationElement root, ControlType controlType);
    AutomationElement[] FindActiveDialogs(AutomationElement root);
    UIA3Automation? UIA3 { get; }
    bool IsUia3 { get; }
    string Name { get; }
}
