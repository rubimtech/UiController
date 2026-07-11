namespace RevitUiController.Core.Models;

public class CommandResult
{
    public string Command { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }
    public SelfDescribingError? ErrorInfo { get; set; }
    public UiStateDiff? Diff { get; set; }
    public string? Screenshot { get; set; }
    public object? Data { get; set; }
    public double DurationMs { get; set; }
}

public class UiStateDiff
{
    public string? ActiveDialog { get; set; }
    public List<string> NewDialogs { get; set; } = new();
    public List<string> ClosedDialogs { get; set; } = new();
    public bool? ActiveTabChanged { get; set; }
    public string? StatusBarText { get; set; }
}

public class SelfDescribingError
{
    public string Code { get; set; } = "";
    public string Query { get; set; } = "";
    public List<string> Suggestions { get; set; } = new();
}
