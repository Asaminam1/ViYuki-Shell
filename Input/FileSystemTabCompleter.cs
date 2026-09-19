using ViYuki.Core;

namespace ViYuki.Input;

public sealed class FileSystemTabCompleter : ITabCompleter
{
    private readonly CommandRegistry _registry;

    public FileSystemTabCompleter(CommandRegistry registry)
    {
        _registry = registry;
    }

    public string? Complete(string input)
    {
        var lastSpace = input.LastIndexOf(' ');
        var commandName = lastSpace < 0 ? input : input[..lastSpace];
        var fragment = lastSpace < 0 ? string.Empty : input[(lastSpace + 1)..];

        if (lastSpace < 0)
        {
            return CompleteCommand(input);
        }

        var resolvedCommand = _registry.ResolveAlias(commandName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty);
        var directoriesOnly = resolvedCommand.Equals("cd", StringComparison.OrdinalIgnoreCase);
        if (!directoriesOnly && !resolvedCommand.Equals("rm", StringComparison.OrdinalIgnoreCase) && !resolvedCommand.Equals("ls", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var completion = CompletePath(fragment, directoriesOnly);
        return completion is null ? null : input[..(lastSpace + 1)] + completion;
    }

    private string? CompleteCommand(string fragment)
    {
        var names = _registry.Commands.Select(command => command.Name).Concat(_registry.Aliases.Keys);
        var matches = names
            .Where(name => name.StartsWith(fragment, StringComparison.OrdinalIgnoreCase))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return matches.Length == 1 ? matches[0] + " " : null;
    }

    private static string? CompletePath(string fragment, bool directoriesOnly)
    {
        var quote = fragment.StartsWith('"');
        var unquoted = quote ? fragment[1..] : fragment;
        var directoryPart = Path.GetDirectoryName(unquoted);
        var filePart = Path.GetFileName(unquoted);
        var searchDirectory = string.IsNullOrEmpty(directoryPart) ? Environment.CurrentDirectory : PathResolver.Resolve(directoryPart);

        if (!Directory.Exists(searchDirectory))
        {
            return null;
        }

        var entries = Directory.EnumerateFileSystemEntries(searchDirectory)
            .Where(path => Path.GetFileName(path).StartsWith(filePart, StringComparison.OrdinalIgnoreCase))
            .Where(path => !directoriesOnly || Directory.Exists(path))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (entries.Length != 1)
        {
            return null;
        }

        var completedName = Path.GetFileName(entries[0]);
        var completed = string.IsNullOrEmpty(directoryPart)
            ? completedName
            : Path.Combine(directoryPart, completedName);

        if (Directory.Exists(entries[0]))
        {
            completed += Path.DirectorySeparatorChar;
        }

        if (completed.Contains(' '))
        {
            completed = '"' + completed.Trim('"') + '"';
        }
        else if (quote)
        {
            completed = '"' + completed + '"';
        }

        return completed;
    }
}
