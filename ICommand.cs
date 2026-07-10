using FlaUI.Core.AutomationElements;

namespace RevitUiController;

public interface ICommand
{
    string Name { get; }
    string Description { get; }
    string Usage { get; }
    Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default);
}
