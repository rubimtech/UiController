using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class Win32ClickCommand : ICommand
{
    public string Name => "win32-click";
    public string Description => "Click a control via Win32 SendMessage fallback: win32-click <name>";
    public string Usage => "win32-click <name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("Win32Commands", "Usage: win32-click <name>");
            return Task.FromResult(1);
        }

        var name = string.Join(" ", args);
        var element = AutomationHelper.FindFirstEnabledVisible(window, name);
        if (element == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        try
        {
            var hWnd = element.Properties.NativeWindowHandle;
            if (hWnd == null || hWnd == IntPtr.Zero)
            {
                element.Click();
                Console.Write(OutputFormatter.FormatResult(new CommandResult
                {
                    Command = "win32-click",
                    Success = true,
                    Data = new { method = "uia_fallback", target = name }
                }, CoreSettings.GlobalOptions));
                return Task.FromResult(0);
            }

            var clicked = Win32Helper.ClickButton(hWnd);
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "win32-click",
                Success = clicked,
                Error = clicked ? null : "Win32 SendMessage click failed",
                Data = new { method = "win32_sendmessage", target = name }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(clicked ? 0 : 1);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("Win32Error", name, [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}

public class Win32EnumCommand : ICommand
{
    public string Name => "win32-enum";
    public string Description => "Enumerate Win32 child windows of Revit main window";
    public string Usage => "win32-enum";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        try
        {
            var hWnd = window.Properties.NativeWindowHandle;
            if (hWnd == null || hWnd == IntPtr.Zero)
            {
                Console.Write(OutputFormatter.FormatError("NoHandle", "window", null, CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }

            var children = Win32Helper.EnumChildWindowsWithText(hWnd);
            var list = children.Select(kvp => new { hWnd = kvp.Key.ToInt64(), text = kvp.Value }).ToList();

            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "win32-enum",
                Success = true,
                Data = new { count = list.Count, windows = list }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("Win32Error", "enum", [ex.Message], CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}
