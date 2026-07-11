namespace RevitUiController.Core.Services;

public class LoggingServiceWrapper : ILoggingService
{
    public void Log(string level, string command, string message) => LoggingService.Log(level, command, message);
    public void Info(string command, string message) => LoggingService.Info(command, message);
    public void Warn(string command, string message) => LoggingService.Warn(command, message);
    public void Error(string command, string message) => LoggingService.Error(command, message);
    public string[] ReadLogs(int tailLines = 50, string? levelFilter = null, DateTime? since = null) => LoggingService.ReadLogs(tailLines, levelFilter, since);
}
