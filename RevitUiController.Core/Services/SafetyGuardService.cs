using FlaUI.Core.AutomationElements;

namespace UiController.Core.Services;

public class SafetyGuardService : ISafetyGuardService
{
    public bool IsNonInteractive
    {
        get => SafetyGuard.IsNonInteractive;
        set => SafetyGuard.IsNonInteractive = value;
    }

    public bool IsDestructive(string commandName, string[] args) => SafetyGuard.IsDestructive(commandName, args);
    public bool ConfirmDestructiveAction(string description, int timeoutSeconds = 10) => SafetyGuard.ConfirmDestructiveAction(description, timeoutSeconds);
    public List<AutomationElement> FindUnexpectedDialogs(AutomationElement revitWindow) => SafetyGuard.FindUnexpectedDialogs(revitWindow);
    public Task DismissWarningDialogs(AutomationElement revitWindow, CancellationToken ct = default) => SafetyGuard.DismissWarningDialogs(revitWindow, ct);
}
