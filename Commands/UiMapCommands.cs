using FlaUI.Core.AutomationElements;
using UiController.Core.Models;
using System.Threading;

namespace RevitUiController.Commands;

public class UiMapLoadCommand : ICommand
{
    public string Name => "uimap-load";
    public string Description => "Load UI Map from YAML file";
    public string Usage => "uimap-load [path]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var path = args.Length > 0 ? string.Join(" ", args) : "";

        bool loaded;
        if (string.IsNullOrEmpty(path))
            loaded = UiMap.TryLoadDefault();
        else
            loaded = UiMap.Load(path);

        if (!loaded)
        {
            Console.Write(OutputFormatter.FormatError("LoadFailed", path, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var result = new CommandResult
        {
            Command = "uimap-load",
            Success = true,
            Data = new { path = UiMap.CurrentPath, entries = UiMap.EntryCount }
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class UiMapSaveCommand : ICommand
{
    public string Name => "uimap-save";
    public string Description => "Save current UI Map config to YAML file";
    public string Usage => "uimap-save [path]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        var path = args.Length > 0 ? string.Join(" ", args) : UiMap.CurrentPath ?? "./uimap.yaml";

        try
        {
            UiMap.Save(path);
            var result = new CommandResult
            {
                Command = "uimap-save",
                Success = true,
                Data = new { path = Path.GetFullPath(path), entries = UiMap.EntryCount }
            };
            if (Program.IsScreenshot)
                result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
            Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError("SaveFailed", ex.Message, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }
    }
}

public class UiMapResolveCommand : ICommand
{
    public string Name => "uimap-resolve";
    public string Description => "Resolve a logical name to candidate selectors";
    public string Usage => "uimap-resolve <logical-name> [--version 2026]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", Usage, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        int? revitYear = null;
        var nameArgs = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--version" && i + 1 < args.Length)
            {
                if (int.TryParse(args[++i], out var year))
                    revitYear = year;
            }
            else
            {
                nameArgs.Add(args[i]);
            }
        }

        var logicalName = string.Join(" ", nameArgs);
        var candidates = UiMap.Resolve(logicalName, revitYear);

        if (candidates.Count == 0)
        {
            var suggestions = UiMap.GetAllEntries().Keys.Take(10).ToList();
            Console.Write(OutputFormatter.FormatError("NotFound", logicalName, suggestions, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var result = new CommandResult
        {
            Command = "uimap-resolve",
            Success = true,
            Data = new
            {
                logicalName,
                revitYear,
                candidates = candidates.Select(c => new
                {
                    c.AutomationId,
                    c.Name,
                    c.Tab,
                    c.ParentPath,
                    c.Fallbacks,
                    c.Source
                }).ToList(),
                count = candidates.Count
            }
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class UiMapRegisterCommand : ICommand
{
    public string Name => "uimap-register";
    public string Description => "Register a new UI Map entry";
    public string Usage => "uimap-register <logical-name> --auto-id <id> [--name <name>] [--tab <tab>]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", Usage, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var logicalName = "";
        var entry = new UiMapEntry();

        var i = 0;
        var nameArgs = new List<string>();
        bool parsingName = true;

        while (i < args.Length)
        {
            if (parsingName && (args[i] == "--auto-id" || args[i] == "--name" || args[i] == "--tab"))
                parsingName = false;

            if (parsingName)
            {
                nameArgs.Add(args[i]);
                i++;
                continue;
            }

            if (args[i] == "--auto-id" && i + 1 < args.Length)
                entry.AutomationId = args[++i];
            else if (args[i] == "--name" && i + 1 < args.Length)
                entry.Name = args[++i];
            else if (args[i] == "--tab" && i + 1 < args.Length)
                entry.Tab = args[++i];

            i++;
        }

        logicalName = string.Join(" ", nameArgs);
        if (string.IsNullOrWhiteSpace(logicalName))
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", "logical-name is required", null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        UiMap.Register(logicalName, entry);

        var result = new CommandResult
        {
            Command = "uimap-register",
            Success = true,
            Data = new
            {
                logicalName,
                entry = new
                {
                    entry.AutomationId,
                    entry.Name,
                    entry.Tab,
                    entry.ParentPath
                }
            }
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class UiMapListCommand : ICommand
{
    public string Name => "uimap-list";
    public string Description => "List all registered UI Map entries";
    public string Usage => "uimap-list [filter]";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (!UiMap.IsLoaded)
        {
            Console.Write(OutputFormatter.FormatError("NotLoaded", "no uimap loaded", null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var filter = args.Length > 0 ? string.Join(" ", args) : null;
        var entries = UiMap.FindEntries(filter);

        var result = new CommandResult
        {
            Command = "uimap-list",
            Success = true,
            Data = new
            {
                filter,
                totalEntries = UiMap.EntryCount,
                matchingEntries = entries.Count,
                entries = entries.Select(kvp => new
                {
                    logicalName = kvp.Key,
                    automationId = kvp.Value.AutomationId,
                    name = kvp.Value.Name,
                    tab = kvp.Value.Tab,
                    parentPath = kvp.Value.ParentPath,
                    hasVersions = kvp.Value.Versions?.Count > 0,
                    fallbackCount = kvp.Value.Fallbacks?.Length ?? 0
                }).ToList()
            }
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class UiMapAutoCommand : ICommand
{
    public string Name => "uimap-auto";
    public string Description => "Find element by name, auto-extract selector info, and register a new UiMap entry";
    public string Usage => "uimap-auto <logical-name> <element-name>";

    public Task<int> ExecuteAsync(AutomationElement revitWindow, string[] args, CancellationToken ct = default)
    {
        if (args.Length < 2)
        {
            Console.Write(OutputFormatter.FormatError("InvalidArgs", Usage, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var logicalName = args[0];
        var elementName = string.Join(" ", args.Skip(1));

        var found = AutomationHelper.FindFirstEnabledVisible(revitWindow, elementName);
        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", elementName, null, Program.GlobalOptions));
            return Task.FromResult(1);
        }

        var autoId = AutomationHelper.SafeGetAutoId(found);
        var name = AutomationHelper.SafeGetName(found);

        var entry = new UiMapEntry
        {
            AutomationId = string.IsNullOrEmpty(autoId) ? null : autoId,
            Name = string.IsNullOrEmpty(name) ? null : name
        };

        UiMap.Register(logicalName, entry);

        var result = new CommandResult
        {
            Command = "uimap-auto",
            Success = true,
            Data = new
            {
                logicalName,
                detected = new
                {
                    automationId = autoId,
                    name
                },
                registered = new
                {
                    entry.AutomationId,
                    entry.Name
                }
            }
        };
        if (Program.IsScreenshot)
            result.Screenshot = ScreenshotHelper.CaptureWindow(revitWindow);
        Console.Write(OutputFormatter.FormatResult(result, Program.GlobalOptions));
        return Task.FromResult(0);
    }
}
