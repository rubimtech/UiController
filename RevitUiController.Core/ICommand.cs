using FlaUI.Core.AutomationElements;

namespace RevitUiController.Core;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    string Usage { get; }
    Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default);
}
