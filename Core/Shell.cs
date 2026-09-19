using ViYuki.Commands;
using ViYuki.Input;
using ViYuki.SystemIntegration;

namespace ViYuki.Core;

public sealed class Shell
{
    private readonly CommandRegistry _registry;
    private readonly CommandParser _parser;
    private readonly ExternalCommandExecutor _externalExecutor;
    private readonly CommandLineReader _reader;
    private bool _exitRequested;

    public Shell(CommandRegistry registry, CommandParser parser, ExternalCommandExecutor externalExecutor)
    {
        _registry = registry;
        _parser = parser;
        _externalExecutor = externalExecutor;
        _reader = new CommandLineReader(new FileSystemTabCompleter(registry));
    }

    public int RunInteractive()
    {
        var context = new CommandContext(isInteractive: true);
        PrepareConsole();
        PrintModernBanner();

        var exitCode = CommandResult.Success;
        while (!_exitRequested && !context.ShouldExit)
        {
            var input = _reader.ReadLine(CreatePrompt());
            if (input is null)
            {
                break;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            exitCode = ExecuteInput(context, input);
        }

        return exitCode;
    }

    public int RunCommand(string[] args)
    {
        var context = new CommandContext(isInteractive: false);
        if (args.Length == 0)
        {
            return CommandResult.Success;
        }

        var parsed = new ParsedCommand(args[0], args.Skip(1).ToArray());
        return ExecuteParsed(context, parsed, BuildCommandLine(args));
    }

    public void RequestExit()
    {
        _exitRequested = true;
    }

    private int ExecuteInput(CommandContext context, string input)
    {
        Console.WriteLine();

        try
        {
            var exitCode = ExecuteParsed(context, _parser.Parse(input), input);
            Console.WriteLine();
            return exitCode;
        }
        catch (CommandParseException ex)
        {
            Console.Error.WriteLine($"viyuki: {ex.Message}");
            Console.WriteLine();
            return CommandResult.InvalidArguments;
        }
    }

    private int ExecuteParsed(CommandContext context, ParsedCommand parsed, string sourceLine)
    {
        if (parsed.Name.Equals("cd..", StringComparison.OrdinalIgnoreCase) && parsed.Arguments.Count == 0)
        {
            parsed = new ParsedCommand("cd", [".."]);
        }

        if (parsed.IsEmpty)
        {
            return CommandResult.Success;
        }

        if (_registry.TryResolve(parsed.Name, out var command))
        {
            try
            {
                return command.Execute(context, parsed.Arguments);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"viyuki: {command.Name}: {ex.Message}");
                return CommandResult.ExecutionError;
            }
        }

        return _externalExecutor.Execute(parsed.Name, parsed.Arguments, sourceLine);
    }

    private static string BuildCommandLine(IReadOnlyList<string> args)
    {
        return string.Join(' ', args.Select(QuoteArgument));
    }

    private static string QuoteArgument(string argument)
    {
        if (argument.Length > 0 && argument.All(character => !char.IsWhiteSpace(character) && character is not '"' and not '&' and not '|' and not '<' and not '>' and not '^'))
        {
            return argument;
        }

        return $"\"{argument.Replace("\"", "\\\"")}\"";
    }

    private static void PrintBanner()
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Magenta;
        Console.WriteLine();
        Console.WriteLine("========================================================================================================================");
        Console.WriteLine("        		ViYuki Terminal Shell");
        Console.WriteLine("========================================================================================================================");
        Console.WriteLine();
        Console.ForegroundColor = previousColor;
    }

    private static Prompt CreatePrompt()
    {
        return new Prompt("╭─ ViYuki", Environment.CurrentDirectory);
    }

    private static void PrepareConsole()
    {
        Console.Title = "ViYuki Shell";
        Console.Clear();
        Console.ForegroundColor = ConsoleColor.Magenta;
    }

    private static void PrintModernBanner()
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Magenta;
        if (!EmbeddedAssets.TryReadText("banner.txt", out var banner))
        {
            Console.Error.WriteLine("ViYuki banner resource is unavailable.");
            Console.ForegroundColor = previousColor;
            return;
        }

        var lines = new List<string>();
        using (var reader = new StringReader(banner))
        {
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                lines.Add(line);
            }
        }

        var widestLine = lines.Count == 0 ? 0 : lines.Max(line => line.Length);
        var leftPadding = Math.Max(0, (GetConsoleWidth() - widestLine) / 2);

        Console.WriteLine();
        foreach (var line in lines)
        {
            if (line.Length > 0)
            {
                Console.Write(new string(' ', leftPadding));
            }

            Console.WriteLine(line);
        }

        if (lines.Count == 0)
        {
            Console.WriteLine();
        }

        Console.WriteLine();
        Console.ForegroundColor = previousColor;
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Math.Max(0, Console.WindowWidth - 1);
        }
        catch (IOException)
        {
            return 0;
        }
    }
}
