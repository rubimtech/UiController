using UiController.Core.Models;

namespace UiController.Core;

public class SessionCheckpoint
{
    public string Name { get; set; } = "";
    public int CommandIndex { get; set; }
    public DateTime Timestamp { get; set; }
}

public class SessionCommandRecord
{
    public DateTime Timestamp { get; set; }
    public string Command { get; set; } = "";
    public object? Args { get; set; }
    public bool Success { get; set; }
    public double DurationMs { get; set; }
}

public static class SessionContext
{
    public static bool IsActive { get; private set; }
    public static string? Name { get; private set; }
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
    private const int MaxCommandHistory = 100;
    private static readonly List<SessionCommandRecord> _commandHistory = new();
    private static readonly List<SessionCheckpoint> _checkpoints = new();

    public static IReadOnlyList<SessionCheckpoint> Checkpoints
    {
        get { lock (Lock) return _checkpoints.ToList(); }
    }

    public static IReadOnlyList<SessionCommandRecord> CommandHistory
    {
        get { lock (Lock) return _commandHistory.ToList(); }
    }

    public static void Begin(string? name = null)
    {
        lock (Lock)
        {
            IsActive = true;
            Name = name;
            ActiveDialog = null;
            ActiveTab = null;
            ActiveViewTab = null;
            DialogStack.Clear();
            Variables.Clear();
            _commandHistory.Clear();
            _checkpoints.Clear();
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
            Name = null;
            ActiveDialog = null;
            ActiveTab = null;
            ActiveViewTab = null;
            DialogStack.Clear();
            Variables.Clear();
            _commandHistory.Clear();
            _checkpoints.Clear();
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
                name = Name,
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

    public static void RecordCommand(string command, object? args, bool success, double durationMs)
    {
        lock (Lock)
        {
            _commandHistory.Add(new SessionCommandRecord
            {
                Timestamp = DateTime.UtcNow,
                Command = command,
                Args = args,
                Success = success,
                DurationMs = durationMs
            });
            while (_commandHistory.Count > MaxCommandHistory)
                _commandHistory.RemoveAt(0);
        }
    }

    public static void SetCheckpoint(string name)
    {
        lock (Lock)
        {
            _checkpoints.Add(new SessionCheckpoint
            {
                Name = name,
                CommandIndex = _commandHistory.Count,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public static int? GetCheckpointIndex(string name)
    {
        lock (Lock)
        {
            var cp = _checkpoints.LastOrDefault(c => c.Name == name);
            return cp?.CommandIndex;
        }
    }

    public static void RemoveLastCommands(int count)
    {
        lock (Lock)
        {
            if (count <= 0 || _commandHistory.Count == 0) return;
            var toRemove = Math.Min(count, _commandHistory.Count);
            _commandHistory.RemoveRange(_commandHistory.Count - toRemove, toRemove);
        }
    }

    public static object FullStatus()
    {
        lock (Lock)
        {
            return new
            {
                name = Name,
                isActive = IsActive,
                activeDialog = ActiveDialog,
                activeTab = ActiveTab,
                activeViewTab = ActiveViewTab,
                dialogStack = DialogStack.Reverse().ToList(),
                variableCount = Variables.Count,
                commandCount = _commandHistory.Count,
                checkpointCount = _checkpoints.Count,
                checkpoints = _checkpoints.Select(c => new { c.Name, c.CommandIndex, c.Timestamp }).ToList(),
                variables = Variables.Keys.ToList(),
                lastCommands = _commandHistory.TakeLast(10).ToList()
            };
        }
    }
}
