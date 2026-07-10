namespace RevitUiController.Models;

public class WindowInfo
{
    public IntPtr Hwnd { get; set; }
    public string Title { get; set; } = "";
    public string ProcessName { get; set; } = "";
    public int Pid { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int MonitorIndex { get; set; }
    public string MonitorName { get; set; } = "";
    public bool IsForeground { get; set; }
    public bool IsMinimized { get; set; }
    public bool IsVisible { get; set; }
}
