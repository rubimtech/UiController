namespace UiController.Core.Models;

public class UiState
{
    public string ActiveWindow { get; set; } = "";
    public List<string> OpenDialogs { get; set; } = new();
    public string? ActiveRibbonTab { get; set; }
    public string? ActiveViewTab { get; set; }
    public string? StatusBarText { get; set; }
}
