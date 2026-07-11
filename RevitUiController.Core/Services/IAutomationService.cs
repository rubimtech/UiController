using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;

namespace RevitUiController.Core.Services;

public interface IAutomationService : IDisposable
{
    Task<bool> ConnectToProcess(int? targetPid = null, string processName = "Revit", int timeoutSec = 30, CancellationToken ct = default);
    Task<bool> ConnectToActive(CancellationToken ct = default);
    Task<bool> ConnectByTitle(string title, int timeoutSec = 30, CancellationToken ct = default);
    void Disconnect();
    bool IsConnected { get; }
    AutomationElement? MainWindow { get; }
    UIA3Automation? Automation { get; }
    int? TargetPid { get; }
}
