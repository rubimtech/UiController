namespace RevitUiController.Revit.Models;

public record RevitInstance(int Pid, int Year, string Title, string ProjectPath, bool IsConnected);
