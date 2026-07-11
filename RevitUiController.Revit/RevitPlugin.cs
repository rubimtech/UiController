using RevitUiController.Core;

namespace RevitUiController.Revit;

public class RevitPlugin : IPlugin
{
    public string Name => "Revit";

    public void RegisterCommands(CommandRegistry registry)
    {
    }
}
