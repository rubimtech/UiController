using RevitUiController.Core;
using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace RevitUiController.Revit.Commands;

[Experimental("RevitApi")]
public class RevitApiCommand : ICommand
{
    public string Name => "revit-api";
    public string Description => "Execute a command via Revit Named Pipe bridge";
    public string Usage => "revit-api <command> [--payload <json>]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("RevitApiCommand", "Usage: revit-api <command> [--payload <json>]");
            return Task.FromResult(1);
        }

        var command = args[0];
        object? payload = null;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--payload" && i + 1 < args.Length)
            {
                try { payload = System.Text.Json.JsonSerializer.Deserialize<object>(args[i + 1]); } catch { payload = new { value = args[i + 1] }; }
                i++;
            }
        }

        payload ??= new { };

        using var client = new PipeBridgeClient();
        if (!client.Connect())
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-api",
                Success = false,
                Error = "Failed to connect to ReVibe Named Pipe. Is Revit running with the plugin loaded?"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var responseJson = client.SendCommand(command, payload);
        if (responseJson == null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-api",
                Success = false,
                Error = "No response from Revit pipe (timeout or disconnect)"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        object? parsed;
        try { parsed = System.Text.Json.JsonSerializer.Deserialize<object>(responseJson); } catch { parsed = responseJson; }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "revit-api",
            Success = true,
            Data = new { command, response = parsed, raw = responseJson }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

[Experimental("RevitApi")]
public class RevitApiSelectCommand : ICommand
{
    public string Name => "revit-select";
    public string Description => "Select elements by IDs in Revit via pipe: revit-select <id1> [id2 ...]";
    public string Usage => "revit-select <element-id> [element-id ...]";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("RevitApiCommand", "Usage: revit-select <element-id> [element-id ...]");
            return Task.FromResult(1);
        }

        using var client = new PipeBridgeClient();
        if (!client.Connect())
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-select",
                Success = false,
                Error = "Failed to connect to ReVibe pipe"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var ids = args.Select(a => long.TryParse(a, out var id) ? id : 0).Where(id => id > 0).ToList();
        var response = client.SendCommand("selectElements", new { ids });
        var ok = response != null;

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "revit-select",
            Success = ok,
            Error = ok ? null : "Failed to select elements",
            Data = new { ids, response }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(ok ? 0 : 1);
    }
}

[Experimental("RevitApi")]
public class RevitApiGetCommand : ICommand
{
    public string Name => "revit-get";
    public string Description => "Get Revit data via pipe: revit-get <query> (elements, views, categories)";
    public string Usage => "revit-get <query>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("RevitApiCommand", "Usage: revit-get <query> (e.g. 'views', 'categories', 'elements')");
            return Task.FromResult(1);
        }

        var query = args[0];
        var command = query switch
        {
            "views" => "getOpenViews",
            "categories" => "getCategories",
            "elements" => "getElements",
            _ => query
        };

        using var client = new PipeBridgeClient();
        if (!client.Connect())
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "revit-get",
                Success = false,
                Error = "Failed to connect to ReVibe pipe"
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        var response = client.SendCommand(command, new { });
        var ok = response != null;

        object? parsed;
        try { parsed = System.Text.Json.JsonSerializer.Deserialize<object>(response!); } catch { parsed = response; }

        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "revit-get",
            Success = ok,
            Error = ok ? null : "No response",
            Data = new { query, response = parsed }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(ok ? 0 : 1);
    }
}
