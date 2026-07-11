namespace UiController.Core.Models;

public class WindowQuery
{
    public int? Pid { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }
    public bool UseActive { get; set; }
}
