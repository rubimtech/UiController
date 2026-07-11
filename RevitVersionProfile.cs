using System.Diagnostics;
using System.Text.RegularExpressions;
using FlaUI.Core.AutomationElements;
using Microsoft.Win32;

namespace RevitUiController;

public record DetectedVersion(int Year, string Source);

public class RevitVersionProfile
{
    public int Year { get; set; }
    public string Tfm { get; set; } = "";
    public string RibbonTabType { get; set; } = "UIFramework.RvtRibbonTab";

    private static readonly Dictionary<int, RevitVersionProfile> Profiles = new()
    {
        [2022] = new() { Year = 2022, Tfm = "net48", RibbonTabType = "Autodesk.Windows.RibbonTab" },
        [2023] = new() { Year = 2023, Tfm = "net48", RibbonTabType = "Autodesk.Windows.RibbonTab" },
        [2024] = new() { Year = 2024, Tfm = "net48", RibbonTabType = "Autodesk.Windows.RibbonTab" },
        [2025] = new() { Year = 2025, Tfm = "net8.0-windows", RibbonTabType = "UIFramework.RvtRibbonTab" },
        [2026] = new() { Year = 2026, Tfm = "net8.0-windows", RibbonTabType = "UIFramework.RvtRibbonTab" },
        [2027] = new() { Year = 2027, Tfm = "net10.0-windows", RibbonTabType = "UIFramework.RvtRibbonTab" },
    };

    public static RevitVersionProfile Detect(AutomationElement revitWindow, Process? process = null)
    {
        var detected = DetectVersion(revitWindow, process);
        return Profiles.TryGetValue(detected.Year, out var p) ? p : Profiles[2026];
    }

    public static DetectedVersion DetectVersion(AutomationElement revitWindow, Process? process = null)
    {
        if (process != null)
        {
            try
            {
                var fvi = process.MainModule?.FileVersionInfo;
                if (fvi != null)
                {
                    var fromFileVersion = DetectFromFileVersion(fvi);
                    if (fromFileVersion.HasValue)
                        return new DetectedVersion(fromFileVersion.Value, "file_version");
                }
            }
            catch { }
        }

        var fromRegistry = DetectFromRegistry();
        if (fromRegistry.HasValue)
            return new DetectedVersion(fromRegistry.Value, "registry");

        var title = revitWindow.Name ?? "";
        var match = Regex.Match(title, @"\b(202[2-7])\b");
        if (match.Success)
            return new DetectedVersion(int.Parse(match.Groups[1].Value), "window_title");

        return new DetectedVersion(2026, "default");
    }

    private static int? DetectFromRegistry()
    {
        foreach (var year in KnownYears)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Autodesk\Revit\{year}\Product");
                if (key != null) return year;
            }
            catch { }
        }
        return null;
    }

    private static int? DetectFromFileVersion(FileVersionInfo fvi)
    {
        var version = fvi.FileVersion ?? fvi.ProductVersion ?? "";
        var parts = version.Split('.');
        if (parts.Length > 0 && int.TryParse(parts[0], out var major))
        {
            if (major >= 2022 && major <= 2027)
                return major;
            if (major >= 22 && major <= 27)
                return 2000 + major;
        }
        return null;
    }

    public static List<int> GetInstalledRevitVersions()
    {
        var versions = new List<int>();
        foreach (var year in KnownYears)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Autodesk\Revit\{year}");
                if (key != null) versions.Add(year);
            }
            catch { }
        }
        return versions;
    }

    private static readonly int[] KnownYears = [2022, 2023, 2024, 2025, 2026, 2027];

    public static int DetectYearFromTitle(string title)
    {
        var match = Regex.Match(title, @"\b(202[2-7])\b");
        return match.Success ? int.Parse(match.Groups[1].Value) : 0;
    }

    public static RevitVersionProfile ForYear(int year)
    {
        return Profiles.TryGetValue(year, out var p) ? p : Profiles[2026];
    }
}
