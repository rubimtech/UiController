using RevitUiController.Core.Models;

namespace RevitUiController.Core;

public static class CoreSettings
{
    public static IApplicationProfile CurrentProfile { get; set; } = new GenericProfile("Revit");
    public static bool IsPretty { get; set; }
    public static bool IsScreenshot { get; set; }
    public static string Verbosity { get; set; } = "normal";
    public static int? TargetPid { get; set; }
    public static string ProcessName
    {
        get => CurrentProfile.ProcessName;
        set { /* kept for backward compat; profile overrides */ }
    }
    public static string? WindowTitle { get; set; }
    public static bool UseActiveWindow { get; set; }
    public static int ConnectTimeoutSec { get; set; } = 30;
    public static bool IsNonInteractive { get; set; }
    public static bool IsUiaOnly { get; set; }
    public static CancellationTokenSource Cts { get; } = new();
    public static WindowSession? CurrentSession { get; set; }
    public static DesktopWindowManager? WindowManager { get; set; }
    public static AutomationEventService? EventService { get; set; }
    public static CommandRegistry? CommandRegistry { get; set; }

    public static ProgramOptions GlobalOptions => new()
    {
        IsPretty = IsPretty,
        IsScreenshot = IsScreenshot,
        Verbosity = Verbosity,
        TargetPid = TargetPid,
        ProcessName = ProcessName,
        WindowTitle = WindowTitle,
        UseActiveWindow = UseActiveWindow,
        ConnectTimeoutSec = ConnectTimeoutSec,
        IsNonInteractive = IsNonInteractive,
        IsUiaOnly = IsUiaOnly,
    };
}
