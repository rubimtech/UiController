using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace UiController.Core.Commands;

public class WadConnectCommand : ICommand
{
    public string Name => "wad-connect";
    public string Description => "Connect to WinAppDriver. Requires WinAppDriver running on localhost:4723";
    public string Usage => "wad-connect";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var hWnd = window.Properties.NativeWindowHandle;
        if (hWnd == null || hWnd == IntPtr.Zero)
        {
            Console.Write(OutputFormatter.FormatError("NoHandle", "window", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        if (WinAppDriverClient.Current != null) WinAppDriverClient.Current.Dispose();
        WinAppDriverClient.Current = new WinAppDriverClient();
        var ok = WinAppDriverClient.Current.Connect(hWnd);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wad-connect",
            Success = ok,
            Error = ok ? null : "Failed to connect to WinAppDriver. Is it running on port 4723?",
            Data = new { hWnd = ((IntPtr)hWnd).ToInt64() }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(ok ? 0 : 1);
    }
}

public class WadFindCommand : ICommand
{
    public string Name => "wad-find";
    public string Description => "Find element via WinAppDriver: wad-find <name> or wad-find --id <automation-id>";
    public string Usage => "wad-find <name> | wad-find --id <automation-id>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("WinAppDriverCommands", "Usage: wad-find <name> | wad-find --id <automation-id>");
            return Task.FromResult(1);
        }

        var hWnd = window.Properties.NativeWindowHandle;
        if (hWnd == null || hWnd == IntPtr.Zero)
        {
            Console.Write(OutputFormatter.FormatError("NoHandle", "window", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var client = WinAppDriverClient.Current;
        if (client == null || !client.IsConnected)
        {
            client = new WinAppDriverClient();
            if (!client.Connect(hWnd))
            {
                Console.Write(OutputFormatter.FormatError("ConnectionFailed", "WinAppDriver", ["Is WinAppDriver running on port 4723?"], CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }
            WinAppDriverClient.Current?.Dispose();
            WinAppDriverClient.Current = client;
        }

        string usingMethod, value;
        if (args[0] == "--id" && args.Length >= 2)
        {
            usingMethod = "accessibility id";
            value = args[1];
        }
        else
        {
            usingMethod = "name";
            value = string.Join(" ", args.Where(a => a != "--id"));
        }

        var elementId = client.FindElement(usingMethod, value);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wad-find",
            Success = elementId != null,
            Error = elementId == null ? $"Element '{value}' not found via WinAppDriver" : null,
            Data = new { method = usingMethod, query = value, elementId }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(elementId != null ? 0 : 1);
    }
}

public class WadClickCommand : ICommand
{
    public string Name => "wad-click";
    public string Description => "Click element via WinAppDriver: wad-click <name>";
    public string Usage => "wad-click <name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("WinAppDriverCommands", "Usage: wad-click <name>");
            return Task.FromResult(1);
        }

        var name = string.Join(" ", args);
        var hWnd = window.Properties.NativeWindowHandle;
        if (hWnd == null || hWnd == IntPtr.Zero)
        {
            Console.Write(OutputFormatter.FormatError("NoHandle", "window", null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var client = WinAppDriverClient.Current;
        if (client == null || !client.IsConnected)
        {
            client = new WinAppDriverClient();
            if (!client.Connect(hWnd))
            {
                Console.Write(OutputFormatter.FormatError("ConnectionFailed", "WinAppDriver", null, CoreSettings.GlobalOptions));
                return Task.FromResult(1);
            }
            WinAppDriverClient.Current?.Dispose();
            WinAppDriverClient.Current = client;
        }

        var elementId = client.FindElement("name", name);
        if (elementId == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var clicked = client.Click(elementId);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "wad-click",
            Success = clicked,
            Error = clicked ? null : $"Failed to click '{name}' via WinAppDriver",
            Data = new { target = name }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(clicked ? 0 : 1);
    }
}
