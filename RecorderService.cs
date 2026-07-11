using System.Text;

namespace RevitUiController;

public static class RecorderService
{
    private static readonly List<string> RecordedActions = new();
    private static bool _isRecording;
    private static string? _outputPath;

    public static void StartRecording(string outputPath)
    {
        _outputPath = outputPath;
        RecordedActions.Clear();
        _isRecording = true;
        RecordedActions.Add("# Recorded by RevitUiController Recorder");
        RecordedActions.Add($"# Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        RecordedActions.Add("");
    }

    public static void Record(string commandLine)
    {
        if (!_isRecording) return;
        RecordedActions.Add(commandLine);
    }

    public static string? StopRecording()
    {
        if (!_isRecording) return null;
        _isRecording = false;

        if (_outputPath == null) return null;

        RecordedActions.Add("");
        RecordedActions.Add($"# Ended: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);
            File.WriteAllLines(_outputPath, RecordedActions, Encoding.UTF8);
            return _outputPath;
        }
        catch (Exception ex)
        {
            LoggingService.Error("RecorderService", $"Failed to save recording: {ex.Message}");
            return null;
        }
    }

    public static bool IsRecording => _isRecording;
    public static int RecordedCount => RecordedActions.Count;

    public static string? GetRecordingPath() => _isRecording ? _outputPath : null;

    public static string? SaveTo(string outputPath)
    {
        if (!_isRecording) return null;

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllLines(outputPath, RecordedActions, Encoding.UTF8);
            return outputPath;
        }
        catch (Exception ex)
        {
            LoggingService.Error("RecorderService", $"Failed to save recording: {ex.Message}");
            return null;
        }
    }
}
