using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace RevitUiController.Core;

public class CompositeAutomationProvider : IAutomationProvider
{
    private readonly IAutomationProvider _primary;
    private readonly IAutomationProvider _fallback;

    public CompositeAutomationProvider(IAutomationProvider primary, IAutomationProvider fallback)
    {
        _primary = primary;
        _fallback = fallback;
    }

    public IAutomationProvider Primary => _primary;
    public IAutomationProvider Fallback => _fallback;
    public UIA3Automation? UIA3 => _primary.UIA3 ?? _fallback.UIA3;
    public bool IsUia3 => _primary.IsUia3 || _fallback.IsUia3;
    public string Name => $"Composite({_primary.Name}+{_fallback.Name})";

    public AutomationElement GetDesktop() => TryBoth(a => a.GetDesktop());
    public AutomationElement GetRootElement(IntPtr hwnd) => TryBoth(a => a.GetRootElement(hwnd));
    public AutomationElement? FindFirst(AutomationElement root, string name, int timeoutMs = 3000) => TryBoth(a => a.FindFirst(root, name, timeoutMs));
    public AutomationElement? FindFirstEnabledVisible(AutomationElement root, string name) => TryBoth(a => a.FindFirstEnabledVisible(root, name));
    public AutomationElement[] FindAllChildren(AutomationElement element) => TryBoth(a => a.FindAllChildren(element));
    public AutomationElement[] FindAllByControlType(AutomationElement root, ControlType controlType) => TryBoth(a => a.FindAllByControlType(root, controlType));
    public AutomationElement[] FindActiveDialogs(AutomationElement root) => TryBoth(a => a.FindActiveDialogs(root));

    private T TryBoth<T>(Func<IAutomationProvider, T> fn)
    {
        try { return fn(_primary); }
        catch
        {
            try { return fn(_fallback); }
            catch { throw; }
        }
    }

    public void Dispose()
    {
        _primary.Dispose();
        _fallback.Dispose();
    }
}
