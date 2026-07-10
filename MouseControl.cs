using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace RevitUiController;

public static class MouseControl
{
    [DllImport("user32.dll")]
    private static extern bool GetDpiForMonitor(IntPtr hmonitor, DpiType dpiType, out uint dpiX, out uint dpiY);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

    [DllImport("user32.dll")]
    private static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    private enum DpiType { Effective = 0, Angular = 1, Raw = 2 }

    private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    private const uint MOUSEEVENTF_LEFTUP = 0x0004;
    private const uint MOUSEEVENTF_MOVE = 0x0001;
    private const uint MOUSEEVENTF_WHEEL = 0x0800;
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    public static double GetDpiScale(IntPtr hwnd)
    {
        try
        {
            var monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
            if (GetDpiForMonitor(monitor, DpiType.Effective, out var dpiX, out _))
                return dpiX / 96.0;
        }
        catch { }
        return 1.0;
    }

    public static (int x, int y) ToPhysical(int uiX, int uiY, double dpiScale)
    {
        return ((int)(uiX * dpiScale), (int)(uiY * dpiScale));
    }

    public static void ClickAt(int x, int y)
    {
        SetCursorPos(x, y);
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(30);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    public static void ClickElement(AutomationElement element)
    {
        try
        {
            var rect = element.BoundingRectangle;
            var hwnd = new IntPtr(Convert.ToInt32(element.Properties.NativeWindowHandle.Value));
            var dpi = hwnd != IntPtr.Zero ? GetDpiScale(hwnd) : 1.0;
            var (px, py) = ToPhysical((int)(rect.X + rect.Width / 2), (int)(rect.Y + rect.Height / 2), dpi);
            ClickAt(px, py);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Mouse.ClickElement failed: {ex.Message}");
        }
    }

    public static void Drag(int x1, int y1, int x2, int y2, int steps = 10)
    {
        SetCursorPos(x1, y1);
        Thread.Sleep(100);
        mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        Thread.Sleep(50);
        for (int i = 1; i <= steps; i++)
        {
            var x = x1 + (x2 - x1) * i / steps;
            var y = y1 + (y2 - y1) * i / steps;
            SetCursorPos(x, y);
            Thread.Sleep(20);
        }
        Thread.Sleep(50);
        mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }

    public static void Scroll(int ticks)
    {
        mouse_event(MOUSEEVENTF_WHEEL, 0, 0, (uint)ticks, UIntPtr.Zero);
    }

    public static (int x, int y) GetPosition()
    {
        GetCursorPos(out var pt);
        return (pt.X, pt.Y);
    }
}
