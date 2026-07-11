namespace RevitUiController.Core.Models;

public class TemplateMetadata
{
    public string Name { get; set; } = "";
    public string FileName { get; set; } = "";
    public int Width { get; set; }
    public int Height { get; set; }
    public double DpiX { get; set; }
    public double DpiY { get; set; }
    public int RevitVersion { get; set; }
    public DateTime Timestamp { get; set; }
    public string Region { get; set; } = "";
    public string? ElementName { get; set; }
}
