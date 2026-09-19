using ViYuki.Core;

namespace ViYuki.Commands;

public sealed class CdCommand : ICommand
{
    public string Name => "cd";

    public string Description => "Changes the current ViYuki working directory.";

    public string Usage => "cd [path]";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
        {
            Console.Error.WriteLine("Usage: cd [path]");
            return CommandResult.InvalidArguments;
        }

        if (args.Count == 0)
        {
            Console.WriteLine(context.CurrentDirectory);
            return CommandResult.Success;
        }

        var target = PathResolver.Resolve(args[0]);
        if (!Directory.Exists(target))
        {
            Console.Error.WriteLine($"cd: no such directory: {args[0]}");
            return CommandResult.ExecutionError;
        }

        try
        {
            context.CurrentDirectory = target;
            return CommandResult.Success;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"cd: access denied: {args[0]}");
            return CommandResult.ExecutionError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"cd: {ex.Message}");
            return CommandResult.ExecutionError;
        }
    }
}
