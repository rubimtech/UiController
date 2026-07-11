using FlaUI.Core.AutomationElements;

namespace UiController.Core;

public static class HighlightHelper
{
    private static Form? _overlayForm;

    public static void Highlight(AutomationElement element, int durationMs = 1500, System.Drawing.Color? color = null)
    {
        try
        {
            var rect = element.BoundingRectangle;
            if (rect.Width <= 0 || rect.Height <= 0) return;

            var c = color ?? System.Drawing.Color.Red;

            _overlayForm?.Close();
            _overlayForm = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ShowInTaskbar = false,
                TopMost = true,
                Location = new System.Drawing.Point((int)rect.X, (int)rect.Y),
                Size = new System.Drawing.Size((int)rect.Width, (int)rect.Height),
                BackColor = c,
                Opacity = 0.3,
                Enabled = false
            };
        }
        catch { }
    }

    public static void Clear()
    {
        try
        {
            _overlayForm?.Close();
            _overlayForm = null;
        }
        catch { }
    }
}
