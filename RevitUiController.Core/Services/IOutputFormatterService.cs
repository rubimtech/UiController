using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;

namespace RevitUiController.Core.Services;

public interface IOutputFormatterService
{
    string FormatResult(CommandResult result, bool pretty = false);
    string FormatResult(CommandResult result, ProgramOptions? options);
    string FormatError(string code, string query, List<string>? suggestions = null, ProgramOptions? options = null);
    ElementInfo FromAutomationElement(AutomationElement el, int depth = 2, int maxDepth = 2);
    List<ElementInfo> FromElementList(IEnumerable<AutomationElement> elements);
    UiState CaptureState(AutomationElement window);
    UiStateDiff ComputeDiff(UiState before, UiState after);
    string? LastOutput { get; }
}
