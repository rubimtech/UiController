using FlaUI.Core.AutomationElements;

namespace RevitUiController;

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

    public static RevitVersionProfile Detect(AutomationElement revitWindow)
    {
        var title = revitWindow.Name ?? "";
        foreach (var (year, profile) in Profiles)
        {
            if (title.Contains(year.ToString())) return profile;
        }
        return Profiles[2026];
    }

    public static RevitVersionProfile ForYear(int year)
    {
        return Profiles.TryGetValue(year, out var p) ? p : Profiles[2026];
    }
}
