using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using UiController.Core;
using UiController.Core.Models;
using UiController.Core.Protocol;

namespace UiController.McpServer;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var pipeName = "RevitUiController";

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pipe" when i + 1 < args.Length:
                    pipeName = args[++i];
                    break;
                case "--help":
                    PrintHelp();
                    return 0;
            }
        }

        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddSingleton(new McpConfig { PipeName = pipeName });
        builder.Services.AddSingleton<DaemonBridge>();

        builder.Services
            .AddMcpServer()
            .WithStdioServerTransport()
            .WithToolsFromAssembly();

        LoggingService.Info("McpServer", "Starting MCP server (stdio transport)");
        await builder.Build().RunAsync();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.Error.WriteLine("""
RevitUiController MCP Server
Options:
  --pipe <name>  Named pipe for daemon bridge (default: RevitUiController)
  --help         Show this help

Mode: stdio transport (connect via MCP client)
Requires: RevitUiController Daemon running with --daemon flag
""");
    }
}

public class McpConfig
{
    public string PipeName { get; set; } = "RevitUiController";
}

public class DaemonBridge
{
    private readonly string _pipeName;
    private DaemonClient? _client;

    private static readonly JsonSerializerOptions PrettyJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DaemonBridge(McpConfig config) { _pipeName = config.PipeName; }

    public bool EnsureConnected()
    {
        if (_client != null && _client.IsConnected) return true;
        _client?.Dispose();
        _client = new DaemonClient(_pipeName);
        return _client.Connect(3000);
    }

    public DaemonResponse? SendAndDeserialize(DaemonRequest request)
    {
        return _client?.SendAndDeserialize(request);
    }

    public string FormatResponse(DaemonResponse? response)
    {
        if (response == null)
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = "Daemon not reachable. Start daemon first.",
                errorInfo = new { code = "connection_failed", codeString = "ConnectionFailed", suggestions = new[] { "Start daemon with --daemon flag" } }
            }, PrettyJson);
        return JsonSerializer.Serialize(response, PrettyJson);
    }
}

[McpServerToolType]
public class RevitUiTools
{
    private readonly DaemonBridge _bridge;

    public RevitUiTools(DaemonBridge bridge) { _bridge = bridge; }

    [McpServerTool, Description("Connect daemon to a Revit process. Must be called first.")]
    public string revit_connect(
        [Description("Process name (default: Revit)")] string? process_name = null,
        [Description("Process ID")] int? pid = null,
        [Description("Window title filter")] string? window_title = null,
        [Description("Connection timeout in seconds")] int? timeout = null) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        {
            Command = "__connect", ProcessName = process_name, Pid = pid,
            WindowTitle = window_title, Timeout = timeout ?? 30
        }));

    [McpServerTool, Description("Click a UI element by name")]
    public string revit_click(
        [Description("Element name to click")] string name,
        [Description("Control type filter (Button, TabItem, Edit...)")] string? type = null,
        [Description("Tab name where element is located")] string? tab = null,
        [Description("Wait time after click in seconds")] int? wait_after = null,
        [Description("Keyboard modifiers (ctrl, shift, alt)")] string? modifiers = null,
        [Description("Auto-retry on failure")] bool? retry = null,
        [Description("Timeout in seconds")] int? timeout = null) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        {
            Command = "click", Args = new List<string> { name },
            Type = type, Tab = tab, WaitAfter = wait_after,
            Modifiers = modifiers, Retry = retry, Timeout = timeout
        }));

    [McpServerTool, Description("Multi-strategy element search by name")]
    public string revit_find(
        [Description("Element name to find")] string name,
        [Description("Control type filter (Button, Tab, Edit...)")] string? type = null,
        [Description("Locale override")] string? locale = null,
        [Description("Search strategy: auto|uia|vision|win32")] string? strategy = null)
    {
        var args = new List<string> { name };
        if (type != null) { args.Add("--type"); args.Add(type); }
        if (locale != null) { args.Add("--locale"); args.Add(locale); }
        if (strategy != null) { args.Add("--strategy"); args.Add(strategy); }
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "ai-find", Args = args }));
    }

    [McpServerTool, Description("Click a ribbon button, optionally on a specific tab or panel")]
    public string revit_ribbon(
        [Description("Button name")] string button,
        [Description("Tab name")] string? tab = null,
        [Description("Panel name")] string? panel = null)
    {
        var args = new List<string> { button };
        if (tab != null) args.Add(tab);
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "ribbon", Args = args, Tab = tab, Panel = panel }));
    }

    [McpServerTool, Description("Read or interact with a PropertySheet dialog")]
    public string revit_ps(
        [Description("Dialog title")] string title,
        [Description("Action: fields|type|check|select|click")] string? action = null,
        [Description("Field label")] string? field = null,
        [Description("Value to set")] string? value = null)
    {
        var args = new List<string> { title };
        if (action != null) args.Add(action);
        if (field != null) args.Add(field);
        if (value != null) args.Add(value);
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "ps", Args = args }));
    }

    [McpServerTool, Description("Type text into a UI element")]
    public string revit_type(
        [Description("Element name")] string name,
        [Description("Text to type")] string text) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "type", Args = new List<string> { name, text } }));

    [McpServerTool, Description("Switch to a view tab")]
    public string revit_switch_view(
        [Description("View name")] string name) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "switch-view", Args = new List<string> { name } }));

    [McpServerTool, Description("Wait for a dialog to appear")]
    public string revit_wait_for(
        [Description("Dialog title")] string title,
        [Description("Timeout in seconds")] int? timeout = null)
    {
        var args = new List<string> { title };
        if (timeout.HasValue) args.Add(timeout.ToString()!);
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "wait-for", Args = args }));
    }

    [McpServerTool, Description("Wait for a dialog to close")]
    public string revit_wait_close(
        [Description("Dialog title")] string title,
        [Description("Timeout in seconds")] int? timeout = null)
    {
        var args = new List<string> { title };
        if (timeout.HasValue) args.Add(timeout.ToString()!);
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "wait-close", Args = args }));
    }

    [McpServerTool, Description("Wait for a UI element to appear")]
    public string revit_wait_element(
        [Description("Element name")] string name,
        [Description("Timeout in seconds")] int? timeout = null)
    {
        var args = new List<string> { name };
        if (timeout.HasValue) args.Add(timeout.ToString()!);
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "wait-element", Args = args }));
    }

    [McpServerTool, Description("Read or interact with a TaskDialog")]
    public string revit_task_dialog(
        [Description("Dialog title")] string title,
        [Description("Action: read|click|expand")] string? action = null,
        [Description("Button name")] string? button = null)
    {
        var args = new List<string> { title };
        if (action != null) args.Add(action);
        if (button != null) args.Add(button);
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "taskdialog", Args = args }));
    }

    [McpServerTool, Description("Execute multiple commands with conditional logic. Each command can have: command, args, onError (stop/skip), if (previous_success/previous_failed), onlyIf (dialog/exists/element/enabled)")]
    public string revit_batch(
        [Description("JSON array of {command, args, onError?, if?, onlyIf?} objects")] string commands)
    {
        List<DaemonRequest>? commandList;
        try
        {
            commandList = JsonSerializer.Deserialize<List<DaemonRequest>>(commands);
        }
        catch
        {
            try
            {
                commandList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(commands)
                    ?.Select(d => new DaemonRequest
                    {
                        Command = d.GetValueOrDefault("command")?.ToString() ?? "",
                        Args = d.GetValueOrDefault("args") is JsonElement je
                            ? je.Deserialize<List<string>>()
                            : d.GetValueOrDefault("args") is List<object> lo
                                ? lo.Select(x => x.ToString()!).ToList()
                                : new List<string>()
                    })
                    .ToList();
            }
            catch { return _bridge.FormatResponse(null); }
        }

        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "__batch", Commands = commandList }));
    }

    [McpServerTool, Description("List all Revit windows and dialogs")]
    public string revit_list_windows() =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "list-windows" }));

    [McpServerTool, Description("List controls in a window or dialog")]
    public string revit_list_controls(
        [Description("Window/dialog name")] string? name = null) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "list-controls", Args = name != null ? new List<string> { name } : new List<string>() }));

    [McpServerTool, Description("Quick snapshot of Revit UI state")]
    public string revit_state() =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "state" }));

    [McpServerTool, Description("Idempotent click — succeed even if element is already gone")]
    public string revit_safe_click(
        [Description("Element name")] string name) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "safe-click", Args = new List<string> { name } }));

    [McpServerTool, Description("Select multiple UI elements by IDs")]
    public string revit_select(
        [Description("Comma-separated element IDs to select")] string element_ids)
    {
        var ids = element_ids.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).ToList();
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "select", ElementIds = ids }));
    }

    [McpServerTool, Description("Get property of a UI element")]
    public string revit_get_property(
        [Description("Element name or ID")] string element_id,
        [Description("Property name to read")] string property_name) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "get-property", Args = new List<string> { element_id, property_name }, PropertyName = property_name }));

    [McpServerTool, Description("Send keyboard shortcut (^c=Ctrl+C, %{F4}=Alt+F4)")]
    public string revit_key_combo(
        [Description("Key combination")] string keys) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "key-combo", Args = new List<string> { keys } }));

    [McpServerTool, Description("Capture screenshot of Revit window as base64")]
    public string revit_screenshot() =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "screenshot-region" }));

    [McpServerTool, Description("List all ribbon tabs and buttons")]
    public string revit_list_tabs(
        [Description("Specific tab to inspect")] string? tab = null) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "ribbon-tabs", Args = tab != null ? new List<string> { tab } : new List<string>() }));

    [McpServerTool, Description("Read Revit status bar text")]
    public string revit_status_bar() =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "statusbar" }));

    [McpServerTool, Description("Query undo stack or perform programmatic undo via Revit API pipe")]
    public string revit_undo(
        [Description("'status' to check undo stack, 'undo' to perform undo, 'checkpoint' to create checkpoint, 'undo-to' to undo to checkpoint")] string action = "status",
        [Description("Number of undo steps")] int? count = null,
        [Description("Checkpoint name (required for checkpoint and undo-to actions)")] string? name = null)
    {
        var args = new List<string>();
        if (name != null) args.Add(name);
        return _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "__undo", Action = action, Count = count ?? 1, Args = args }));
    }

    [McpServerTool, Description("Create a named checkpoint for undo context")]
    public string revit_checkpoint(
        [Description("Checkpoint name")] string name) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "checkpoint", Args = new List<string> { name } }));

    [McpServerTool, Description("Undo last N commands")]
    public string revit_undo_last(
        [Description("Number of commands to undo")] int? count = null) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "undo-last", Count = count ?? 1 }));

    [McpServerTool, Description("Undo to a named checkpoint")]
    public string revit_undo_to(
        [Description("Checkpoint name")] string name) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "undo-to", Args = new List<string> { name } }));

    [McpServerTool, Description("Read recent UI events from daemon's event watcher")]
    public string revit_events(
        [Description("Max wait time in seconds")] int? timeout = null,
        [Description("Max events to return")] int? max_events = null) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "__events", Timeout = timeout ?? 5, MaxEvents = max_events ?? 10 }));

    [McpServerTool, Description("Check if daemon is alive and connected to Revit")]
    public string revit_ping() =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "__ping" }));

    [McpServerTool, Description("Start a named session for maintaining context between calls. Session tracks variables, dialog stack, and command history.")]
    public string revit_session_begin(
        [Description("Session name (e.g. 'create-wall')")] string name = "default") =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "session-begin", Args = new List<string> { name } }));

    [McpServerTool, Description("End the current session and clear all session state")]
    public string revit_session_end() =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "session-end" }));

    [McpServerTool, Description("Get full session status: name, active dialog, dialog stack, variables, command history")]
    public string revit_session_status() =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest { Command = "session-status" }));

    [McpServerTool, Description("Set a session variable for passing context between commands")]
    public string revit_session_set(
        [Description("Variable name")] string name,
        [Description("Variable value")] string value) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "session-set", Args = new List<string> { name, value } }));

    [McpServerTool, Description("Get a session variable value")]
    public string revit_session_get(
        [Description("Variable name")] string name) =>
        _bridge.FormatResponse(_bridge.SendAndDeserialize(new DaemonRequest
        { Command = "session-get", Args = new List<string> { name } }));
}
