using System.Drawing;
using System.Drawing.Imaging;
using FlaUI.Core.AutomationElements;

namespace RevitUiController.Core.Services;

public class ScreenshotService : IScreenshotService
{
    public Bitmap? CaptureBitmap(AutomationElement window) => ScreenshotHelper.CaptureBitmap(window);
    public Bitmap? CaptureBitmap(int x, int y, int width, int height) => ScreenshotHelper.CaptureBitmap(x, y, width, height);
    public string? CaptureBase64(int x, int y, int width, int height) => ScreenshotHelper.CaptureBase64(x, y, width, height);
    public string? CaptureWindow(AutomationElement window) => ScreenshotHelper.CaptureWindow(window);
    public string? CaptureRegion(int x, int y, int w, int h) => ScreenshotHelper.CaptureRegion(x, y, w, h);

    public string? SaveToFile(int x, int y, int width, int height, string path)
    {
        var bitmap = CaptureBitmap(x, y, width, height);
        if (bitmap == null) return null;
        bitmap.Save(path, ImageFormat.Png);
        bitmap.Dispose();
        return path;
    }
}
