using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using static RevitUiController.Core.AutomationHelper;

namespace RevitUiController.Core;

public static class SafetyGuard
{
    private static readonly HashSet<string> DestructiveCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "click", "safe-click", "ribbon", "revit-api", "revit-select",
        "win32-click", "canvas-click",
    };

    private static readonly HashSet<string> DestructivePatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "delete", "purge", "overwrite", "remove", "erase",
        "удалить", "очистить", "purgeunused",
    };

    private static readonly HashSet<string> AllowedDialogPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        "warning", "error", "предупреждение", "ошибка",
        "confirm", "подтверждение", "question", "вопрос"
    };

    public static bool IsDestructive(string commandName, string[] args)
    {
        if (DestructiveCommands.Contains(commandName))
            return true;
        var combined = commandName + " " + string.Join(" ", args);
        return DestructivePatterns.Any(p => combined.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsNonInteractive { get; set; }

    public static bool ConfirmDestructiveAction(string description, int timeoutSeconds = 10)
    {
        Console.Error.WriteLine($"[SAFETY] Destructive action detected: {description}");

        if (IsNonInteractive || !Environment.UserInteractive)
        {
            Console.Error.WriteLine("[SAFETY] Non-interactive mode — rejecting.");
            return false;
        }

        Console.Error.WriteLine("[SAFETY] Confirm? Type 'yes' to proceed, anything else to cancel:");

        try
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            var task = Task.Run(() => Console.ReadLine() ?? "", cts.Token);
            if (task.Wait(timeoutSeconds * 1000, cts.Token))
            {
                var input = task.Result;
                return input.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("[SAFETY] Timeout — rejecting.");
        }

        return false;
    }

    public static List<AutomationElement> FindUnexpectedDialogs(AutomationElement revitWindow)
    {
        var unexpected = new List<AutomationElement>();
        try
        {
            var dialogs = FindActiveDialogs(revitWindow);
            foreach (var d in dialogs)
            {
                try
                {
                    var name = d.Name ?? "";
                    if (!AllowedDialogPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    unexpected.Add(d);
                }
                catch { }
            }
        }
        catch { }
        return unexpected;
    }

    public static async Task DismissWarningDialogs(AutomationElement revitWindow, CancellationToken ct = default)
    {
        var dialogs = FindActiveDialogs(revitWindow);
        foreach (var d in dialogs)
        {
            try
            {
                var name = d.Name ?? "";
                if (!AllowedDialogPatterns.Any(p => name.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    continue;
                foreach (var c in SafeGetChildren(d, 3000))
                {
                    try
                    {
                        if (c.ControlType == ControlType.Button)
                        {
                            var btnName = c.Name ?? "";
                            if (btnName.Equals("OK", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("ОК", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Close", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Закрыть", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Yes", StringComparison.OrdinalIgnoreCase) ||
                                btnName.Equals("Да", StringComparison.OrdinalIgnoreCase))
                            {
                                if (c.IsEnabled)
                                {
                                    c.Click();
                                    await Task.Delay(500, ct);
                                }
                                break;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }
    }
}
