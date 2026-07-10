using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class ScreenshotRegionCommand : ICommand
{
    public string Name => "screenshot-region";
    public string Description => "Capture a screenshot of a specific screen region. Usage: screenshot-region <x> <y> <w> <h>";
    public string Usage => "screenshot-region <x> <y> <w> <h>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: screenshot-region <x> <y> <w> <h>");
            return Task.FromResult(1);
        }

        if (!int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y) ||
            !int.TryParse(args[2], out var w) || !int.TryParse(args[3], out var h))
        {
            Console.Error.WriteLine("Invalid coordinates: x y w h must be integers");
            return Task.FromResult(1);
        }

        var b64 = ScreenshotHelper.CaptureRegion(x, y, w, h);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "screenshot-region", Success = b64 != null,
            Error = b64 == null ? "Screenshot failed" : null,
            Screenshot = b64,
            Data = new { region = new { x, y, w, h } }
        }, Program.IsPretty));
        return Task.FromResult(b64 != null ? 0 : 1);
    }
}

public class HighlightRegionCommand : ICommand
{
    public string Name => "highlight-region";
    public string Description => "Highlight a screen region with a colored overlay. Usage: highlight-region <x> <y> <w> <h> [ms]";
    public string Usage => "highlight-region <x> <y> <w> <h> [ms]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 4)
        {
            Console.Error.WriteLine("Usage: highlight-region <x> <y> <w> <h> [ms]");
            return Task.FromResult(1);
        }

        if (!int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y) ||
            !int.TryParse(args[2], out var w) || !int.TryParse(args[3], out var h))
        {
            Console.Error.WriteLine("Invalid coordinates: x y w h must be integers");
            return Task.FromResult(1);
        }

        var ms = args.Length > 4 && int.TryParse(args[4], out var d) ? d : 2000;

        try
        {
            using var form = new TransparentOverlayForm(x, y, w, h, ms);
            form.ShowDialog();

            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "highlight-region", Success = true,
                Data = new { region = new { x, y, w, h }, durationMs = ms }
            }, Program.IsPretty));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("HighlightFailed", $"{x},{y},{w},{h}", [ex.Message], Program.IsPretty));
            return Task.FromResult(1);
        }
    }
}

internal class TransparentOverlayForm : Form, IDisposable
{
    private readonly int _durationMs;

    public TransparentOverlayForm(int x, int y, int w, int h, int durationMs)
    {
        _durationMs = durationMs;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(x, y);
        Size = new Size(w, h);
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = Color.Red;
        Opacity = 0.3;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _ = Task.Run(async () =>
        {
            await Task.Delay(_durationMs);
            try { Invoke(() => Close()); } catch { }
        });
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            cp.ExStyle |= 0x00000020; // WS_EX_TRANSPARENT
            cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
            return cp;
        }
    }
}
