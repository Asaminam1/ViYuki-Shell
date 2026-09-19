using ViYuki.Core;

namespace ViYuki.Commands;

public sealed class ClearCommand : ICommand
{
    public string Name => "clear";

    public string Description => "Clears the console screen.";

    public IReadOnlyList<string> Aliases => ["cls"];

    public string Usage => "clear";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            Console.Error.WriteLine("Usage: clear");
            return CommandResult.InvalidArguments;
        }

        Console.Clear();
        return CommandResult.Success;
    }
}
