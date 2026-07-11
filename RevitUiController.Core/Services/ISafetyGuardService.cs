using FlaUI.Core.AutomationElements;

namespace UiController.Core.Services;

public interface ISafetyGuardService
{
    bool IsDestructive(string commandName, string[] args);
    bool IsNonInteractive { get; set; }
    bool ConfirmDestructiveAction(string description, int timeoutSeconds = 10);
    List<AutomationElement> FindUnexpectedDialogs(AutomationElement revitWindow);
    Task DismissWarningDialogs(AutomationElement revitWindow, CancellationToken ct = default);
}
