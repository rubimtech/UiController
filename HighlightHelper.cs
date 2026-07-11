using FlaUI.Core.AutomationElements;

namespace RevitUiController;

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

            _overlayForm.Show();
            System.Windows.Forms.Timer? timer = null;
            timer = new System.Windows.Forms.Timer { Interval = durationMs };
            timer.Tick += (_, _) =>
            {
                timer?.Stop();
                timer?.Dispose();
                try { _overlayForm?.Close(); } catch (Exception ex) { LoggingService.Warn("Safe", $"Highlight timer close: {ex.Message}"); }
                _overlayForm = null;
            };
            timer.Start();
        }
        catch (Exception ex)
        {
            LoggingService.Error("HighlightHelper", $"Highlight failed: {ex.Message}");
        }
    }

    public static void Clear()
    {
        try
        {
            _overlayForm?.Close();
            _overlayForm = null;
        }
        catch (Exception ex) { LoggingService.Warn("Safe", $"Highlight Clear: {ex.Message}"); }
    }
}
