namespace UiController.Core.Services;

public interface ILoggingService
{
    void Log(string level, string command, string message);
    void Info(string command, string message);
    void Warn(string command, string message);
    void Error(string command, string message);
    string[] ReadLogs(int tailLines = 50, string? levelFilter = null, DateTime? since = null);
}
