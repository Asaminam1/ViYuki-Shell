using ViYuki.Core;

namespace ViYuki.Commands;

public sealed class RmCommand : ICommand
{
    public string Name => "rm";

    public string Description => "Removes files or directories.";

    public string Usage => "rm [-r] [-f] path [path ...]";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        var options = RmOptions.Parse(args);
        if (!options.IsValid)
        {
            Console.Error.WriteLine(options.Error);
            Console.Error.WriteLine($"Usage: {Usage}");
            return CommandResult.InvalidArguments;
        }

        var exitCode = CommandResult.Success;
        foreach (var path in options.Paths)
        {
            var result = RemoveOne(path, options.Force);
            if (result != CommandResult.Success)
            {
                exitCode = result;
            }
        }

        return exitCode;
    }

    private static int RemoveOne(string inputPath, bool force)
    {
        var target = PathResolver.Resolve(inputPath);
        try
        {
            if (File.Exists(target))
            {
                ClearReadOnlyFile(target);
                File.Delete(target);
                return CommandResult.Success;
            }

            if (Directory.Exists(target))
            {
                ClearReadOnlyAttributes(target);
                Directory.Delete(target, recursive: true);
                return CommandResult.Success;
            }

            if (!force)
            {
                Console.Error.WriteLine($"rm: cannot remove '{inputPath}': no such file or directory");
                return CommandResult.ExecutionError;
            }

            return CommandResult.Success;
        }
        catch (UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"rm: cannot remove '{inputPath}': access denied");
            return CommandResult.ExecutionError;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"rm: cannot remove '{inputPath}': {ex.Message}");
            return CommandResult.ExecutionError;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"rm: cannot remove '{inputPath}': {ex.Message}");
            return CommandResult.ExecutionError;
        }
    }

    private static void ClearReadOnlyFile(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private static void ClearReadOnlyAttributes(string directory)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(directory, "*", SearchOption.AllDirectories))
        {
            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            }
        }

        var directoryAttributes = File.GetAttributes(directory);
        if ((directoryAttributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(directory, directoryAttributes & ~FileAttributes.ReadOnly);
        }
    }

    private sealed class RmOptions
    {
        public bool Force { get; private init; }

        public IReadOnlyList<string> Paths { get; private init; } = Array.Empty<string>();

        public bool IsValid { get; private init; }

        public string Error { get; private init; } = string.Empty;

        public static RmOptions Parse(IReadOnlyList<string> args)
        {
            var force = false;
            var paths = new List<string>();

            foreach (var arg in args)
            {
                if (arg.StartsWith("-", StringComparison.Ordinal) && arg.Length > 1)
                {
                    foreach (var option in arg.Skip(1))
                    {
                        switch (option)
                        {
                            case 'r':
                            case 'R':
                                break;
                            case 'f':
                                force = true;
                                break;
                            default:
                                return Invalid($"rm: unknown option '-{option}'");
                        }
                    }

                    continue;
                }

                paths.Add(arg);
            }

            if (paths.Count == 0)
            {
                return Invalid("rm: missing operand");
            }

            return new RmOptions
            {
                Force = force,
                Paths = paths,
                IsValid = true
            };
        }

        private static RmOptions Invalid(string error)
        {
            return new RmOptions
            {
                IsValid = false,
                Error = error
            };
        }
    }
}
