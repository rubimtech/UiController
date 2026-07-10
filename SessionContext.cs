namespace RevitUiController;

public static class SessionContext
{
    public static bool IsActive { get; private set; }
    public static string? ActiveDialog { get; private set; }
    public static string? ActiveTab { get; set; }
    public static string? ActiveViewTab { get; set; }
    public static Stack<string> DialogStack { get; } = new();
    public static Dictionary<string, object> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static long? ActiveHwnd { get; set; }
    public static int? ActiveMonitor { get; set; }
    public static string? ActiveProcessName { get; set; }
    public static int? ActivePid { get; set; }
    public static string? ActiveMonitorName { get; set; }

    private static readonly object Lock = new();

    public static void Begin()
    {
        lock (Lock)
        {
            IsActive = true;
            ActiveDialog = null;
            ActiveTab = null;
            ActiveViewTab = null;
            DialogStack.Clear();
            Variables.Clear();
            ActiveHwnd = null;
            ActiveMonitor = null;
            ActiveProcessName = null;
            ActivePid = null;
            ActiveMonitorName = null;
        }
    }

    public static void End()
    {
        lock (Lock)
        {
            IsActive = false;
            ActiveDialog = null;
            ActiveTab = null;
            ActiveViewTab = null;
            DialogStack.Clear();
            Variables.Clear();
            ActiveHwnd = null;
            ActiveMonitor = null;
            ActiveProcessName = null;
            ActivePid = null;
            ActiveMonitorName = null;
        }
    }

    public static void PushDialog(string title)
    {
        lock (Lock)
        {
            if (ActiveDialog != null)
                DialogStack.Push(ActiveDialog);
            ActiveDialog = title;
        }
    }

    public static void PopDialog()
    {
        lock (Lock)
        {
            if (DialogStack.Count > 0)
                ActiveDialog = DialogStack.Pop();
            else
                ActiveDialog = null;
        }
    }

    public static void SetVariable(string name, object value)
    {
        lock (Lock)
        {
            Variables[name] = value;
        }
    }

    public static object? GetVariable(string name)
    {
        lock (Lock)
        {
            return Variables.TryGetValue(name, out var value) ? value : null;
        }
    }

    public static void ResetDialogContext()
    {
        lock (Lock)
        {
            ActiveDialog = null;
            DialogStack.Clear();
        }
    }

    public static object Status()
    {
        lock (Lock)
        {
            return new
            {
                isActive = IsActive,
                activeDialog = ActiveDialog,
                activeTab = ActiveTab,
                activeViewTab = ActiveViewTab,
                dialogStackDepth = DialogStack.Count,
                dialogStack = DialogStack.Reverse().ToList(),
                variableCount = Variables.Count,
                variableNames = Variables.Keys.ToList(),
                activeHwnd = ActiveHwnd,
                activeMonitor = ActiveMonitor,
                activeProcessName = ActiveProcessName,
                activePid = ActivePid,
                activeMonitorName = ActiveMonitorName
            };
        }
    }
}
