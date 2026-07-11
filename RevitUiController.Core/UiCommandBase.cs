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
            Console.Write(OutputFormatter.FormatError("Exception", Name, new[] { ex.Message }.ToList(), Options));
            return 1;
        }
    }

    protected abstract Task<CommandResult> ExecuteInternalAsync(AutomationElement window, string[] args, CancellationToken ct);

    protected AutomationElement? FindElement(AutomationElement root, string name)
    {
        return AutomationHelper.FindFirstEnabledVisible(root, name);
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
