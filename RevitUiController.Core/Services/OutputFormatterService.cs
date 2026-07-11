using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;

namespace RevitUiController.Core.Services;

public class OutputFormatterService : IOutputFormatterService
{
    public string FormatResult(CommandResult result, bool pretty = false) => OutputFormatter.FormatResult(result, pretty);
    public string FormatResult(CommandResult result, ProgramOptions? options) => OutputFormatter.FormatResult(result, options);
    public string FormatError(string code, string query, List<string>? suggestions = null, ProgramOptions? options = null)
        => OutputFormatter.FormatError(code, query, suggestions, options);
    public ElementInfo FromAutomationElement(AutomationElement el, int depth = 2, int maxDepth = 2) => OutputFormatter.FromAutomationElement(el, depth, maxDepth);
    public List<ElementInfo> FromElementList(IEnumerable<AutomationElement> elements) => OutputFormatter.FromElementList(elements);
    public UiState CaptureState(AutomationElement window) => OutputFormatter.CaptureState(window);
    public UiStateDiff ComputeDiff(UiState before, UiState after) => OutputFormatter.ComputeDiff(before, after);
    public string? LastOutput
    {
        get => OutputFormatter.LastOutput;
    }
}
