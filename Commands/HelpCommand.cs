using ViYuki.Core;

namespace ViYuki.Commands;

public sealed class HelpCommand : ICommand
{
    private readonly CommandRegistry _registry;

    public HelpCommand(CommandRegistry registry)
    {
        _registry = registry;
    }

    public string Name => "help";

    public string Description => "Shows available commands or help for one command.";

    public string Usage => "help [command]";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
        {
            Console.Error.WriteLine("Usage: help [command]");
            return CommandResult.InvalidArguments;
        }

        if (args.Count == 1)
        {
            var commandName = _registry.ResolveAlias(args[0]);
            if (!_registry.TryResolve(commandName, out var command))
            {
                Console.Error.WriteLine($"help: unknown command '{args[0]}'");
                return CommandResult.CommandNotFound;
            }

            Console.WriteLine($"{command.Name} - {command.Description}");
            Console.WriteLine($"Usage: {command.Usage}");
            var aliases = _registry.Aliases
                .Where(pair => pair.Value.Equals(command.Name, StringComparison.OrdinalIgnoreCase))
                .Select(pair => pair.Key)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (aliases.Length > 0)
            {
                Console.WriteLine($"Aliases: {string.Join(", ", aliases)}");
            }

            return CommandResult.Success;
        }

        Console.WriteLine("ViYuki commands:");
        foreach (var command in _registry.Commands)
        {
            Console.WriteLine($"  {command.Name,-8} {command.Description}");
        }

        if (_registry.Aliases.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Aliases:");
            foreach (var alias in _registry.Aliases.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {alias.Key,-8} {alias.Value}");
            }
        }

        return CommandResult.Success;
    }
}
