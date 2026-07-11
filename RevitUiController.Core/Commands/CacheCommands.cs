using FlaUI.Core.AutomationElements;
using RevitUiController.Core.Models;
using System.Threading;

namespace RevitUiController.Core.Commands;

public class CacheFindCommand : ICommand
{
    public string Name => "cached-find";
    public string Description => "Find control by name with caching (faster on repeated calls)";
    public string Usage => "cached-find <name>";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        if (args.Length == 0)
        {
            LoggingService.Error("CacheCommands", "Usage: cached-find <name>");
            return Task.FromResult(1);
        }

        var name = string.Join(" ", args);
        var cached = ElementCache.Get(name);
        if (cached != null)
        {
            Console.Write(OutputFormatter.FormatResult(new CommandResult
            {
                Command = "cached-find",
                Success = true,
                Data = new { source = "cache", name }
            }, CoreSettings.GlobalOptions));
            return Task.FromResult(0);
        }

        var found = AutomationHelper.FindFirstEnabledVisible(window, name);
        if (found == null)
        {
            Console.Write(OutputFormatter.FormatError("NotFound", name, null, CoreSettings.GlobalOptions));
            return Task.FromResult(1);
        }

        ElementCache.Add(name, found, window);
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "cached-find",
            Success = true,
            Data = new { source = "fresh", name }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class CacheClearCommand : ICommand
{
    public string Name => "cache-clear";
    public string Description => "Clear element cache";
    public string Usage => "cache-clear";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        var before = ElementCache.Count;
        ElementCache.InvalidateAll();
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "cache-clear",
            Success = true,
            Data = new { cleared = before }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}

public class CacheStatsCommand : ICommand
{
    public string Name => "cache-stats";
    public string Description => "Show element cache statistics";
    public string Usage => "cache-stats";

    public Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        Console.Write(OutputFormatter.FormatResult(new CommandResult
        {
            Command = "cache-stats",
            Success = true,
            Data = new { cachedElements = ElementCache.Count }
        }, CoreSettings.GlobalOptions));
        return Task.FromResult(0);
    }
}
