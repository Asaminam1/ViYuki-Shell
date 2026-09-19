using ViYuki.Core;

namespace ViYuki.Commands;

public interface ICommand
{
    string Name { get; }

    string Description { get; }

    IReadOnlyList<string> Aliases => Array.Empty<string>();

    string Usage => Name;

    int Execute(CommandContext context, IReadOnlyList<string> args);
}
