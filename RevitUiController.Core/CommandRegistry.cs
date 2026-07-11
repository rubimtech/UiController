namespace UiController.Core;

public class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Type> _commandTypes = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICommand cmd) { _commands[cmd.Name] = cmd; }

    public void Register<T>() where T : ICommand
    {
        var name = GetCommandNameFromType(typeof(T));
        _commandTypes[name] = typeof(T);
    }

    public void RegisterType(Type type)
    {
        if (!typeof(ICommand).IsAssignableFrom(type) || type.IsAbstract)
            throw new ArgumentException($"Type {type.FullName} must implement ICommand and not be abstract.");
        var name = GetCommandNameFromType(type);
        _commandTypes[name] = type;
    }

    public void RegisterAlias(string alias, string commandName) { _aliases[alias] = commandName; }

    public ICommand? GetCommand(string name)
    {
        if (_commands.TryGetValue(name, out var cmd)) return cmd;
        if (_aliases.TryGetValue(name, out var resolved) && _commands.TryGetValue(resolved, out cmd)) return cmd;
        return null;
    }

    public Type? GetCommandType(string name)
    {
        if (_commandTypes.TryGetValue(name, out var type)) return type;
        if (_aliases.TryGetValue(name, out var resolved) && _commandTypes.TryGetValue(resolved, out type)) return type;
        return null;
    }

    public IEnumerable<ICommand> AllCommands => _commands.Values;
    public IEnumerable<KeyValuePair<string, string>> AllAliases => _aliases;
    public IEnumerable<KeyValuePair<string, Type>> AllCommandTypes => _commandTypes;

    private static string GetCommandNameFromType(Type type)
    {
        var instance = Activator.CreateInstance(type) as ICommand;
        return instance?.Name ?? type.Name.Replace("Command", "").ToLowerInvariant();
    }
}
