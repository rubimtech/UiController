using FlaUI.Core.AutomationElements;
using UiController.Core.Models;

namespace UiController.Core;

public abstract class UiCommandBase : ICommand
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string Usage { get; }
    public ProgramOptions Options => CoreSettings.GlobalOptions;

    public async Task<int> ExecuteAsync(AutomationElement window, string[] args, CancellationToken ct = default)
    {
        try
        {
            var before = OutputFormatter.CaptureState(window);
            var result = await ExecuteInternalAsync(window, args, ct);
            var after = OutputFormatter.CaptureState(window);

            result.Command = Name;
            result.Diff = OutputFormatter.ComputeDiff(before, after);

            Console.Write(OutputFormatter.FormatResult(result, Options));
            return result.Success ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Write(OutputFormatter.FormatError(Models.ErrorCode.InternalError, Name, new[] { ex.Message }.ToList(), Options));
            return 1;
        }
    }

    protected abstract Task<CommandResult> ExecuteInternalAsync(AutomationElement window, string[] args, CancellationToken ct);

    protected AutomationElement? FindElement(AutomationElement root, string name)
    {
        var result = AutomationHelper.FindFirstEnabledVisible(root, name);
        if (result != null) return result;

        var normalized = LocaleMap.Normalize(name);
        if (normalized != name)
            result = AutomationHelper.FindFirstEnabledVisible(root, normalized);
        if (result != null) return result;

        return null;
    }

    protected SelfDescribingError BuildElementNotFoundError(string name, AutomationElement? root)
    {
        var similar = root != null ? AutomationHelper.FindSimilarElementNames(root, name) : new();
        var suggestions = new List<string>
        {
            "Try 'ai-find \"" + name + "\"' for multi-strategy search",
            "Try 'list-controls' to see available elements",
            "Check locale: '" + name + "' in Russian may be localized"
        };
        if (similar.Count > 0)
            suggestions.Add("Similar elements: " + string.Join(", ", similar.Take(3)));
        return new SelfDescribingError
        {
            Code = Models.ErrorCode.ElementNotFound,
            CodeString = Models.ErrorCode.ElementNotFound.ToString(),
            Query = name,
            Suggestions = suggestions,
            AvailableElements = similar.Count > 0 ? similar : null
        };
    }

    protected void RequireArgs(string[] args, int min)
    {
        if (args.Length < min)
            throw new ArgumentException($"Usage: {Usage}");
    }

    protected T GetFlag<T>(string[] args, string flag, T defaultValue) where T : IParsable<T>
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == flag && T.TryParse(args[i + 1], null, out var value))
                return value;
        }
        return defaultValue;
    }

    protected bool HasFlag(string[] args, string flag)
    {
        return args.Contains(flag);
    }
}
