using System.Runtime.InteropServices;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

namespace RevitUiController.Commands;

public class CanvasClickCommand : ICommand
{
    public string Name => "canvas-click";
    public string Description => "Click at a position within the Revit graphics viewport: canvas-click <x> <y> [--relative]";
    public string Usage => "canvas-click <x> <y> [--relative]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2 || !int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y))
        {
            Console.Error.WriteLine("Usage: canvas-click <x> <y> [--relative]");
            return 1;
        }

        var relative = args.Contains("--relative");

        if (relative)
        {
            var viewport = FindViewport(revitWindow);
            if (viewport != null)
            {
                try
                {
                    var rect = viewport.BoundingRectangle;
                    x += (int)rect.X;
                    y += (int)rect.Y;
                }
                catch { }
            }
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        await MouseControl.ClickAt(x, y, ct);
        var after = OutputFormatter.CaptureState(revitWindow);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "canvas-click",
            Success = true,
            Data = new { x, y, relative },
            Diff = OutputFormatter.ComputeDiff(before, after)
        }, Program.GlobalOptions));
        return 0;
    }

    public static AutomationElement? FindViewport(AutomationElement root)
    {
        try
        {
            var stack = new Queue<AutomationElement>();
            stack.Enqueue(root);
            while (stack.Count > 0)
            {
                var el = stack.Dequeue();
                try
                {
                    var autoId = el.AutomationId ?? "";
                    var name = el.Name ?? "";
                    if (autoId.Contains("GraphicsView", StringComparison.OrdinalIgnoreCase) ||
                        autoId.Contains("RvtCanvas", StringComparison.OrdinalIgnoreCase))
                        return el;
                    if (name.Contains("GraphicsView", StringComparison.OrdinalIgnoreCase))
                        return el;
                    foreach (var c in SafeGetChildren(el, 2000))
                        stack.Enqueue(c);
                }
                catch { }
            }
        }
        catch { }
        return null;
    }
}

public class CanvasDragCommand : ICommand
{
    public string Name => "canvas-drag";
    public string Description => "Drag within viewport: canvas-drag <x1> <y1> <x2> <y2> [--relative]";
    public string Usage => "canvas-drag <x1> <y1> <x2> <y2> [--relative]";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 4 ||
            !int.TryParse(args[0], out var x1) ||
            !int.TryParse(args[1], out var y1) ||
            !int.TryParse(args[2], out var x2) ||
            !int.TryParse(args[3], out var y2))
        {
            Console.Error.WriteLine("Usage: canvas-drag <x1> <y1> <x2> <y2> [--relative]");
            return 1;
        }

        var relative = args.Contains("--relative");

        if (relative)
        {
            var viewport = FindViewport(revitWindow);
            if (viewport != null)
            {
                try
                {
                    var rect = viewport.BoundingRectangle;
                    x1 += (int)rect.X;
                    y1 += (int)rect.Y;
                    x2 += (int)rect.X;
                    y2 += (int)rect.Y;
                }
                catch { }
            }
        }

        var before = OutputFormatter.CaptureState(revitWindow);
        await MouseControl.Drag(x1, y1, x2, y2, ct: ct);
        var after = OutputFormatter.CaptureState(revitWindow);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "canvas-drag",
            Success = true,
            Data = new { from = new { x = x1, y = y1 }, to = new { x = x2, y = y2 }, relative },
            Diff = OutputFormatter.ComputeDiff(before, after)
        }, Program.GlobalOptions));
        return 0;
    }

    private static AutomationElement? FindViewport(AutomationElement root) => CanvasClickCommand.FindViewport(root);
}

public class CanvasZoomCommand : ICommand
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);

    public string Name => "canvas-zoom";
    public string Description => "Zoom in/out in viewport: canvas-zoom <factor> (positive=zoom in, negative=zoom out)";
    public string Usage => "canvas-zoom <factor>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var factor))
        {
            Console.Error.WriteLine("Usage: canvas-zoom <factor> (positive=in, negative=out)");
            return 1;
        }

        var viewport = FindViewport(revitWindow);
        AutomationElement? target = viewport ?? revitWindow;

        try
        {
            var rect = target.BoundingRectangle;
            var centerX = (int)(rect.X + rect.Width / 2);
            var centerY = (int)(rect.Y + rect.Height / 2);

            var dpi = MouseControl.GetDpiScale(target.Properties.NativeWindowHandle);
            (var px, var py) = MouseControl.ToPhysical(centerX, centerY, dpi);

            var before = OutputFormatter.CaptureState(revitWindow);
            SetCursorPos(px, py);
            await Task.Delay(100, ct);
            MouseControl.Scroll(factor * 120);
            var after = OutputFormatter.CaptureState(revitWindow);

            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "canvas-zoom",
                Success = true,
                Data = new { factor, centerX = px, centerY = py },
                Diff = OutputFormatter.ComputeDiff(before, after)
            }, Program.GlobalOptions));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("CanvasZoomError", factor.ToString(), [ex.Message], Program.GlobalOptions));
            return 1;
        }
    }

    private static AutomationElement? FindViewport(AutomationElement root) => CanvasClickCommand.FindViewport(root);
}

public class CanvasScreenshotCommand : ICommand
{
    public string Name => "canvas-screenshot";
    public string Description => "Capture screenshot of the graphics viewport";
    public string Usage => "canvas-screenshot";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var viewport = FindViewport(revitWindow) ?? revitWindow;
        var ss = ScreenshotHelper.CaptureWindow(viewport);

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "canvas-screenshot",
            Success = ss != null,
            Error = ss == null ? "Failed to capture viewport screenshot" : null,
            Data = new { hasScreenshot = ss != null },
            Screenshot = ss
        }, Program.GlobalOptions));
        return ss != null ? 0 : 1;
    }

    private static AutomationElement? FindViewport(AutomationElement root) => CanvasClickCommand.FindViewport(root);
}
