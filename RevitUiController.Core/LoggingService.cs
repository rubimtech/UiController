using System.Diagnostics;
using System.IO;

namespace RevitUiController.Core;

public static class LoggingService
{
    private static readonly string LogDir;
    private static readonly string LogFile;
    private static readonly object Lock = new();

    static LoggingService()
    {
        LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ReVibe", "UiController", "logs");
        Directory.CreateDirectory(LogDir);
        LogFile = Path.Combine(LogDir, $"uictrl_{DateTime.Now:yyyyMMdd}.log");
    }

    public static void Log(string level, string command, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var line = $"[{timestamp}] [{level}] [{command}] {message}";
        lock (Lock)
        {
            File.AppendAllText(LogFile, line + Environment.NewLine);
        }
    }

    public static void Info(string command, string message) => Log("INFO", command, message);
    public static void Warn(string command, string message) => Log("WARN", command, message);
    public static void Error(string command, string message) => Log("ERROR", command, message);

    public static string[] ReadLogs(int tailLines = 50, string? levelFilter = null, DateTime? since = null)
    {
        if (!File.Exists(LogFile)) return [];

        var lines = File.ReadAllLines(LogFile);
        var filtered = lines.AsEnumerable();

        if (levelFilter != null)
            filtered = filtered.Where(l => l.Contains($"[{levelFilter}]", StringComparison.OrdinalIgnoreCase));
        if (since.HasValue)
            filtered = filtered.Where(l =>
            {
                if (l.Length < 24) return false;
                if (DateTime.TryParse(l[1..25], out var ts)) return ts >= since.Value;
                return true;
            });

        if (tailLines > 0)
            filtered = filtered.TakeLast(tailLines);

        return filtered.ToArray();
    }

    public static string[] ReadPluginLogs(int tailLines = 50, string? levelFilter = null)
    {
        var pluginLogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RuBIMtech", "ReVibe", "logs");

        if (!Directory.Exists(pluginLogDir))
        {
            pluginLogDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ReVibe", "logs");
        }

        if (!Directory.Exists(pluginLogDir))
            return ["Plugin log directory not found."];

        var latestLog = new DirectoryInfo(pluginLogDir)
            .GetFiles("*.log")
            .OrderByDescending(f => f.LastWriteTime)
            .FirstOrDefault();

        if (latestLog == null)
            return ["No plugin log files found."];

        var allLines = File.ReadAllLines(latestLog.FullName);
        var filtered = allLines.AsEnumerable();

        if (levelFilter != null)
            filtered = filtered.Where(l => l.Contains($"[{levelFilter}]", StringComparison.OrdinalIgnoreCase) ||
                                          l.Contains(levelFilter, StringComparison.OrdinalIgnoreCase));

        if (tailLines > 0)
            filtered = filtered.TakeLast(tailLines);

        return filtered.ToArray();
    }
}
