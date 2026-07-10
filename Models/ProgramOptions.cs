namespace RevitUiController.Models;

public record ProgramOptions
{
    public bool IsPretty { get; init; }
    public bool IsScreenshot { get; init; }
    public string Verbosity { get; init; } = "normal";
    public int? TargetPid { get; init; }
    public string ProcessName { get; init; } = "Revit";
    public string? WindowTitle { get; init; }
    public bool UseActiveWindow { get; init; }
    public int ConnectTimeoutSec { get; init; } = 30;
    public bool IsNonInteractive { get; init; }
    public bool IsUiaOnly { get; init; }

    public static ProgramOptions FromGlobalFlags() => new()
    {
        IsPretty = Program.IsPretty,
        IsScreenshot = Program.IsScreenshot,
        Verbosity = Program.Verbosity,
        TargetPid = Program.TargetPid,
        ProcessName = Program.ProcessName,
        WindowTitle = Program.WindowTitle,
        UseActiveWindow = Program.UseActiveWindow,
        ConnectTimeoutSec = Program.ConnectTimeoutSec,
        IsNonInteractive = SafetyGuard.IsNonInteractive,
        IsUiaOnly = Program.IsUiaOnly,
    };
}
