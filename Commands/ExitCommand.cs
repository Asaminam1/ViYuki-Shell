using ViYuki.Core;

namespace ViYuki.Commands;

public sealed class ExitCommand : ICommand
{
    public string Name => "exit";

    public string Description => "Exits the interactive ViYuki shell.";

    public string Usage => "exit";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            Console.Error.WriteLine("Usage: exit");
            return CommandResult.InvalidArguments;
        }

        context.RequestExit();
        return CommandResult.Success;
    }
}
