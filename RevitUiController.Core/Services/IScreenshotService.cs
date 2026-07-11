using System.Drawing;
using FlaUI.Core.AutomationElements;

namespace RevitUiController.Core.Services;

public interface IScreenshotService
{
    Bitmap? CaptureBitmap(AutomationElement window);
    Bitmap? CaptureBitmap(int x, int y, int width, int height);
    string? CaptureBase64(int x, int y, int width, int height);
    string? CaptureWindow(AutomationElement window);
    string? CaptureRegion(int x, int y, int w, int h);
    string? SaveToFile(int x, int y, int width, int height, string path);
}
