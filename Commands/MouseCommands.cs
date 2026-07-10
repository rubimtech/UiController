using FlaUI.Core.AutomationElements;
using RevitUiController.Models;
using static RevitUiController.AutomationHelper;

namespace RevitUiController.Commands;

public class MouseClickCommand : ICommand
{
    public string Name => "mouse-click";
    public string Description => "Click at screen coordinates or on an element: mouse-click <name> or mouse-click <x> <y>";
    public string Usage => "mouse-click <x> <y> | mouse-click <element-name>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: mouse-click <x> <y> | mouse-click <element-name>");
            return Task.FromResult(1);
        }

        if (args.Length >= 2 && int.TryParse(args[0], out var x) && int.TryParse(args[1], out var y))
        {
            MouseControl.ClickAt(x, y);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "mouse-click",
                Success = true,
                Data = new { x, y }
            }, Program.IsPretty));
            return Task.FromResult(0);
        }

        var name = string.Join(" ", args);
        var before = OutputFormatter.CaptureState(revitWindow);
        var element = FindFirstEnabledVisible(revitWindow, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, Program.IsPretty));
            return Task.FromResult(1);
        }

        MouseControl.ClickElement(element);
        var after = OutputFormatter.CaptureState(revitWindow);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-click",
            Success = true,
            Data = new { name, @using = "coordinate_click" },
            Diff = OutputFormatter.ComputeDiff(before, after)
        }, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class MouseDragCommand : ICommand
{
    public string Name => "mouse-drag";
    public string Description => "Drag from (x1,y1) to (x2,y2)";
    public string Usage => "mouse-drag <x1> <y1> <x2> <y2>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length < 4 ||
            !int.TryParse(args[0], out var x1) ||
            !int.TryParse(args[1], out var y1) ||
            !int.TryParse(args[2], out var x2) ||
            !int.TryParse(args[3], out var y2))
        {
            Console.Error.WriteLine("Usage: mouse-drag <x1> <y1> <x2> <y2>");
            return Task.FromResult(1);
        }

        MouseControl.Drag(x1, y1, x2, y2);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-drag",
            Success = true,
            Data = new { from = new { x = x1, y = y1 }, to = new { x = x2, y = y2 } }
        }, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class MouseScrollCommand : ICommand
{
    public string Name => "mouse-scroll";
    public string Description => "Scroll mouse wheel: positive = up, negative = down";
    public string Usage => "mouse-scroll <ticks>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out var ticks))
        {
            Console.Error.WriteLine("Usage: mouse-scroll <ticks> (negative = down, positive = up)");
            return Task.FromResult(1);
        }

        MouseControl.Scroll(ticks);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-scroll",
            Success = true,
            Data = new { ticks }
        }, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class MousePosCommand : ICommand
{
    public string Name => "mouse-pos";
    public string Description => "Get current cursor position";
    public string Usage => "mouse-pos";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        var pos = MouseControl.GetPosition();
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-pos",
            Success = true,
            Data = new { x = pos.x, y = pos.y }
        }, Program.IsPretty));
        return Task.FromResult(0);
    }
}

public class MouseTypeCommand : ICommand
{
    public string Name => "mouse-type";
    public string Description => "Type text using SendKeys at current focus";
    public string Usage => "mouse-type <text>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: mouse-type <text>");
            return Task.FromResult(1);
        }

        var text = string.Join(" ", args);
        System.Windows.Forms.SendKeys.SendWait(text);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "mouse-type",
            Success = true,
            Data = new { text }
        }, Program.IsPretty));
        return Task.FromResult(0);
    }
}
