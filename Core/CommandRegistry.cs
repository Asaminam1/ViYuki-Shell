using ViYuki.Commands;

namespace ViYuki.Core;

public sealed class CommandRegistry
{
    private readonly Dictionary<string, ICommand> _commands = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliases;

    public CommandRegistry(IDictionary<string, string> aliases)
    {
        _aliases = new Dictionary<string, string>(aliases, StringComparer.OrdinalIgnoreCase);
    }

    public IEnumerable<ICommand> Commands => _commands.Values
        .Distinct()
        .OrderBy(command => command.Name, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Aliases => _aliases;

    public void Register(ICommand command)
    {
        _commands[command.Name] = command;

        foreach (var alias in command.Aliases)
        {
            _aliases.TryAdd(alias, command.Name);
        }
    }

    public bool TryResolve(string name, out ICommand command)
    {
        if (_aliases.TryGetValue(name, out var target))
        {
            name = target;
        }

        return _commands.TryGetValue(name, out command!);
    }

    public string ResolveAlias(string name)
    {
        return _aliases.TryGetValue(name, out var target) ? target : name;
    }
}
