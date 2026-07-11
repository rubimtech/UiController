namespace RevitUiController.Core;

public interface IPlugin
{
    string Name { get; }
    void RegisterCommands(CommandRegistry registry);
}
