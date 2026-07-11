namespace RevitUiController.Core;

public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICommand cmd) { _commands[cmd.Name] = cmd; }

    public void RegisterAlias(string alias, string commandName) { _aliases[alias] = commandName; }

    public ICommand? GetCommand(string name)
    {
        if (_commands.TryGetValue(name, out var cmd)) return cmd;
        if (_aliases.TryGetValue(name, out var resolved) && _commands.TryGetValue(resolved, out cmd)) return cmd;
        return null;
    }

    public IEnumerable<ICommand> AllCommands => _commands.Values;
    public IEnumerable<KeyValuePair<string, string>> AllAliases => _aliases;
}
