using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using static UiController.Core.AutomationHelper;

namespace RevitUiController.Commands;

public class MouseClickCommand : ICommand
{
    public string Name => "mouse-click";
    public string Description => "Click at screen coordinates or on an element: mouse-click <name> or mouse-click <x> <y>";
    public string Usage => "mouse-click <x> <y> | mouse-click <element-name>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("MouseCommands", "Usage: mouse-click <x> <y> | mouse-click <element-name>");
            return 1;
        }

        if (args.Length >= 2 && int.TryParse(args[0], out var x) && int.TryParse(args[1], out var y))
        {
            await MouseControl.ClickAt(x, y, ct);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "mouse-click",
                Success = true,
                Data = new { x, y }
            }, Program.GlobalOptions));
            return 0;
        }

        var name = string.Join(" ", args);
        var before = OutputFormatter.CaptureState(revitWindow);
        var element = FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.GlobalOptions));
            return 1;
        }

        await MouseControl.ClickElement(element, ct);
        var after = OutputFormatter.CaptureState(revitWindow);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-click",
            Success = true,
            Data = new { name, @using = "coordinate_click" },
            Diff = OutputFormatter.ComputeDiff(before, after)
        }, Program.GlobalOptions));
        return 0;
    }
}

public class MouseDragCommand : ICommand
{
    public string Name => "mouse-drag";
    public string Description => "Drag from (x1,y1) to (x2,y2)";
    public string Usage => "mouse-drag <x1> <y1> <x2> <y2>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 4 ||
            !int.TryParse(args[0], out var x1) ||
            !int.TryParse(args[1], out var y1) ||
            !int.TryParse(args[2], out var x2) ||
            !int.TryParse(args[3], out var y2))
        {
            LoggingService.Error("MouseCommands", "Usage: mouse-drag <x1> <y1> <x2> <y2>");
            return 1;
        }

        await MouseControl.Drag(x1, y1, x2, y2, ct: ct);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-drag",
            Success = true,
            Data = new { from = new { x = x1, y = y1 }, to = new { x = x2, y = y2 } }
        }, Program.GlobalOptions));
        return 0;
    }
}

public class MouseScrollCommand : ICommand
{
    public string Name => "mouse-scroll";
    public string Description => "Scroll mouse wheel: positive = up, negative = down";
    public string Usage => "mouse-scroll <ticks>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var ticks))
        {
            LoggingService.Error("MouseCommands", "Usage: mouse-scroll <ticks> (negative = down, positive = up)");
            return 1;
        }

        MouseControl.Scroll(ticks);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-scroll",
            Success = true,
            Data = new { ticks }
        }, Program.GlobalOptions));
        return 0;
    }
}

public class MousePosCommand : ICommand
{
    public string Name => "mouse-pos";
    public string Description => "Get current cursor position";
    public string Usage => "mouse-pos";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var pos = MouseControl.GetPosition();
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-pos",
            Success = true,
            Data = new { x = pos.x, y = pos.y }
        }, Program.GlobalOptions));
        return 0;
    }
}

public class MouseTypeCommand : ICommand
{
    public string Name => "mouse-type";
    public string Description => "Type text using SendKeys at current focus";
    public string Usage => "mouse-type <text>";

    public async Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("MouseCommands", "Usage: mouse-type <text>");
            return 1;
        }

        var text = string.Join(" ", args);
        System.Windows.Forms.SendKeys.SendWait(text);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-type",
            Success = true,
            Data = new { text }
        }, Program.GlobalOptions));
        return 0;
    }
}
