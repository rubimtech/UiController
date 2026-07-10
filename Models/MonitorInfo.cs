namespace RevitUiController.Models;

public class MonitorInfo
{
    public int Index { get; set; }
    public string DeviceName { get; set; } = "";
    public bool IsPrimary { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int WorkX { get; set; }
    public int WorkY { get; set; }
    public int WorkWidth { get; set; }
    public int WorkHeight { get; set; }
    public double DpiScale { get; set; } = 1.0;
}
