using System.Diagnostics;
using ViYuki.Core;

namespace ViYuki.SystemIntegration;

public sealed class ExternalCommandExecutor
{
    public int Execute(string command, IReadOnlyList<string> args, string sourceLine)
    {
        return ExecuteThroughCommandProcessor(command, sourceLine);
    }

    private static int ExecuteThroughCommandProcessor(string command, string commandLine)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            WorkingDirectory = Environment.CurrentDirectory,
            UseShellExecute = false,
            Arguments = $"/d /c {UnwrapOuterQuotes(commandLine)}"
        };

        process.Start();
        process.WaitForExit();

        if (process.ExitCode == 9009)
        {
            Console.Error.WriteLine($"viyuki: command not found: {command}");
            return CommandResult.CommandNotFound;
        }

        return process.ExitCode;
    }

    private static string UnwrapOuterQuotes(string commandLine)
    {
        if (commandLine.Length >= 2 && commandLine[0] == '"' && commandLine[^1] == '"')
        {
            return commandLine[1..^1];
        }

        return commandLine;
    }
}
