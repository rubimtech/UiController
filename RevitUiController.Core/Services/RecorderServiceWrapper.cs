namespace UiController.Core.Services;

public class RecorderServiceWrapper : IRecorderService
{
    public bool IsRecording => RecorderService.IsRecording;
    public int RecordedCount => RecorderService.RecordedCount;

    public void StartRecording(string outputPath) => RecorderService.StartRecording(outputPath);
    public void Record(string commandLine) => RecorderService.Record(commandLine);
    public string? GetRecordingPath() => RecorderService.GetRecordingPath();
    public string? SaveTo(string? path = null) => RecorderService.SaveTo(path);
    public string? StopRecording() => RecorderService.StopRecording();
}
