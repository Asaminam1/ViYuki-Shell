using ViYuki.Core;

namespace ViYuki.Commands;

public sealed class LsCommand : ICommand
{
    public string Name => "ls";

    public string Description => "Lists directory contents.";

    public IReadOnlyList<string> Aliases => ["ll"];

    public string Usage => "ls [path]";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 1)
        {
            Console.Error.WriteLine("Usage: ls [path]");
            return CommandResult.InvalidArguments;
        }

        var target = args.Count == 0 ? context.CurrentDirectory : PathResolver.Resolve(args[0]);
        if (!Directory.Exists(target))
        {
            Console.Error.WriteLine($"ls: no such directory: {(args.Count == 0 ? target : args[0])}");
            return CommandResult.ExecutionError;
        }

        try
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(target).Order(StringComparer.OrdinalIgnoreCase))
            {
                PrintEntry(entry);
            }

            return CommandResult.Success;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"ls: access denied: {target}");
            return CommandResult.ExecutionError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"ls: {ex.Message}");
            return CommandResult.ExecutionError;
        }
    }

    private static void PrintEntry(string path)
    {
        var previousColor = Console.ForegroundColor;
        var name = Path.GetFileName(path);

        if (Directory.Exists(path))
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"{name}\\");
        }
        else
        {
            Console.ForegroundColor = previousColor;
            Console.WriteLine(name);
        }

        Console.ForegroundColor = previousColor;
    }
}
