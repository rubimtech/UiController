namespace UiController.Core.Services;

public interface IRecorderService
{
    bool IsRecording { get; }
    int RecordedCount { get; }
    void StartRecording(string outputPath);
    void Record(string commandLine);
    string? GetRecordingPath();
    string? SaveTo(string? path = null);
    string? StopRecording();
}
