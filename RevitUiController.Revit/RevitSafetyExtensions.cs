using System.Diagnostics;
using System.IO;
using UiController.Core;

namespace RevitUiController.Revit;

public static class RevitSafetyExtensions
{
    public static bool IsRevitProcessAlive(Process? process)
    {
        if (process == null) return false;
        try { return !process.HasExited; }
        catch { return false; }
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
            LoggingService.Warn("RevitSafety", "Revit executable not found.");
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
            LoggingService.Error("RevitSafety", $"Failed to start Revit: {ex.Message}");
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
