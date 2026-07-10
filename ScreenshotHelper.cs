using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;

namespace RevitUiController;

public static class ScreenshotHelper
{
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc, int nXSrc, int nYSrc, uint dwRop);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDesktopWindow();

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private const uint SRCCOPY = 0x00CC0020;

    public static Bitmap? CaptureBitmap(AutomationElement window)
    {
        try
        {
            var r = window.BoundingRectangle;
            return CaptureBitmap((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
        }
        catch { return null; }
    }

    public static Bitmap? CaptureBitmap(int x, int y, int width, int height)
    {
        try
        {
            var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            var hdc1 = g.GetHdc();
            var hdc2 = GetWindowDC(GetDesktopWindow());
            BitBlt(hdc1, 0, 0, width, height, hdc2, x, y, SRCCOPY);
            ReleaseDC(GetDesktopWindow(), hdc2);
            g.ReleaseHdc(hdc1);
            return bitmap;
        }
        catch { return null; }
    }

    public static string? CaptureBase64(int x, int y, int width, int height)
    {
        try
        {
            using var bitmap = new Bitmap(width, height);
            using var g = Graphics.FromImage(bitmap);
            var hdc1 = g.GetHdc();
            var hdc2 = GetWindowDC(GetDesktopWindow());
            BitBlt(hdc1, 0, 0, width, height, hdc2, x, y, SRCCOPY);
            ReleaseDC(GetDesktopWindow(), hdc2);
            g.ReleaseHdc(hdc1);

            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            return Convert.ToBase64String(ms.ToArray());
        }
        catch
        {
            return null;
        }
    }

    public static string? CaptureWindow(AutomationElement window)
    {
        try
        {
            var r = window.BoundingRectangle;
            return CaptureBase64((int)r.X, (int)r.Y, (int)r.Width, (int)r.Height);
        }
        catch { return null; }
    }
}
