using System.IO;
using System.Text;

namespace UiController.Core;

public static class RecorderService
{
    private static readonly List<string> RecordedActions = new();
    private static bool _isRecording;
    private static string? _outputPath;

    public static bool IsRecording => _isRecording;
    public static int RecordedCount => RecordedActions.Count;

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

    public static string? GetRecordingPath() => _outputPath;

    public static string? SaveTo(string? path = null)
    {
        path ??= _outputPath;
        if (path == null) return null;
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); } catch { }
        File.WriteAllText(path, string.Join(Environment.NewLine, RecordedActions));
        return path;
    }

    public static string? StopRecording()
    {
        if (!_isRecording) return null;
        _isRecording = false;
        var output = string.Join(Environment.NewLine, RecordedActions);
        if (_outputPath != null)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!); } catch { }
            File.WriteAllText(_outputPath, output);
        }
        return output;
    }
}
