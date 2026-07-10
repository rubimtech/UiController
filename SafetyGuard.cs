using System.Diagnostics;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using static RevitUiController.AutomationHelper;

namespace RevitUiController;

public static class SafetyGuard
{
    private static readonly HashSet<string> DestructivePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "delete", "purge", "overwrite", "remove", "erase",
        "удалить", "очистить", "purgeunused", "deletewall",
        "deleteelement", "removeelement"
    };

    private static readonly HashSet<string> AllowedDialogPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "warning", "error", "предупреждение", "ошибка",
        "confirm", "подтверждение", "question", "вопрос"
    };

    public static bool IsDestructive(string commandName, string[] args)
    {
        var combined = commandName + " " + string.Join(" ", args);
        return DestructivePatterns.Any(p => combined.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsNonInteractive { get; set; }

    public static bool ConfirmDestructiveAction(string description, int timeoutSeconds = 10)
    {
        Console.Error.WriteLine($"[SAFETY] Destructive action detected: {description}");

        if (IsNonInteractive || !Environment.UserInteractive)
        {
            Console.Error.WriteLine("[SAFETY] Non-interactive mode — rejecting.");
            return false;
        }

        Console.Error.WriteLine("[SAFETY] Confirm? Type 'yes' to proceed, anything else to cancel:");

        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var task = Task.Run(() => Console.ReadLine() ?? "", cts.Token);
            if (task.Wait(timeoutSeconds * 1000, cts.Token))
            {
                var input = task.Result;
                return input.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[SAFETY] Timeout — rejecting.");
        }

        return false;
    }

    public static List<AutomationElement> FindUnexpectedDialogs(AutomationElement revitWindow)
    {
        var unexpected = new List<AutomationElement>();
        try
        {
            var dialogs = FindActiveDialogs(revitWindow);
            foreach (var d in dialogs)
            {
                try
                {
                    var name = d.Name ?? "";
                    if (!AllowedDialogPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    unexpected.Add(d);
                }
                catch (Exception ex) { LoggingService.Warn("Safe", $"FindUnexpectedDialogs inner: {ex.Message}"); }
            }
        }
        catch (Exception ex) { LoggingService.Warn("Safe", $"FindUnexpectedDialogs outer: {ex.Message}"); }
        return unexpected;
    }

    public static async Task DismissWarningDialogs(AutomationElement revitWindow, CancellationToken ct = default)
    {
        var dialogs = FindActiveDialogs(revitWindow);
        foreach (var d in dialogs)
        {
            try
            {
                var name = d.Name ?? "";
                if (!AllowedDialogPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    continue;
                foreach (var c in SafeGetChildren(d, 3000))
                {
                    try
                    {
                        if (c.ControlType == ControlType.Button)
                        {
                            var btnName = c.Name ?? "";
                            if (btnName.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("ОК", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Close", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Закрыть", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Да", StringComparison.OrdinalIgnoreCase))
                            {
                                if (c.IsEnabled)
                                {
                                    c.Click();
                                    await Task.Delay(500, ct);
                                }
                                break;
                            }
                        }
                    }
                    catch (Exception ex) { LoggingService.Warn("Safe", $"DismissWarningDialogs button click: {ex.Message}"); }
                }
            }
            catch (Exception ex) { LoggingService.Warn("Safe", $"DismissWarningDialogs inner: {ex.Message}"); }
        }
    }

    public static bool IsRevitProcessAlive(Process? process)
    {
        if (process == null) return false;
        try { return !process.HasExited; }
        catch (Exception ex) { LoggingService.Warn("Safe", $"IsRevitProcessAlive: {ex.Message}"); return false; }
    }

    public static Process? GetRevitProcess()
    {
        return Process.GetProcessesByName("Revit")
            .FirstOrDefault(p => p.MainWindowHandle != IntPtr.Zero);
    }

    public static Process? StartRevit(string? revitPath = null)
    {
        if (revitPath == null)
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\Autodesk\Revit 2026\Revit.exe",
                @"C:\Program Files\Autodesk\Revit 2025\Revit.exe",
                @"C:\Program Files\Autodesk\Revit 2024\Revit.exe",
                @"C:\Program Files\Autodesk\Revit 2027\Revit.exe",
            };
            revitPath = possiblePaths.FirstOrDefault(File.Exists);
        }

        if (revitPath == null || !File.Exists(revitPath))
        {
            Console.Error.WriteLine("Revit executable not found.");
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo(revitPath) { UseShellExecute = true };
            var process = Process.Start(psi);
            Console.WriteLine($"Started Revit: {revitPath} (PID={process?.Id})");
            return process;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start Revit: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> WaitForRevitReady(int timeoutMs = 120000, CancellationToken ct = default)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();
            var process = GetRevitProcess();
            if (process != null && process.MainWindowHandle != IntPtr.Zero && !string.IsNullOrEmpty(process.MainWindowTitle))
                return true;
            await Task.Delay(2000, ct);
        }
        return false;
    }
}
