using System.Diagnostics;
using ViYuki.Core;

namespace ViYuki.Commands;

public sealed class KillCommand : ICommand
{
    private const int ExitTimeoutMilliseconds = 5000;

    public string Name => "kill";

    public string Description => "Forcefully terminate a Windows process.";

    public string Usage => "kill -n name\n  kill -id PID\n\nOptions:\n  -n,  --name    Kill process(es) by name\n  -id, --id      Kill process by PID";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count != 2)
        {
            PrintUsage();
            return CommandResult.InvalidArguments;
        }

        return args[0].ToLowerInvariant() switch
        {
            "-n" or "--name" => KillByName(args[1]),
            "-id" or "--id" => KillById(args[1]),
            _ => InvalidSyntax()
        };
    }

    private static int KillById(string inputPid)
    {
        if (!int.TryParse(inputPid, out var pid) || pid <= 0)
        {
            PrintUsage();
            return CommandResult.InvalidArguments;
        }

        if (pid == Environment.ProcessId)
        {
            Console.Error.WriteLine("Cannot kill the current ViYuki process.");
            return CommandResult.ExecutionError;
        }

        try
        {
            using var process = Process.GetProcessById(pid);
            return Terminate(process) switch
            {
                TerminationResult.Killed => CommandResult.Success,
                TerminationResult.AccessDenied => PrintAccessDenied(pid),
                TerminationResult.NotFound => PrintNotFound(pid),
                _ => CommandResult.ExecutionError
            };
        }
        catch (ArgumentException)
        {
            return PrintNotFound(pid);
        }
    }

    private static int KillByName(string inputName)
    {
        var processName = NormalizeProcessName(inputName);
        if (processName is null)
        {
            PrintUsage();
            return CommandResult.InvalidArguments;
        }

        Process[] processes;
        try
        {
            processes = Process.GetProcessesByName(processName);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Console.Error.WriteLine($"kill: cannot look up process '{inputName}': {ex.Message}");
            return CommandResult.ExecutionError;
        }

        if (processes.Length == 0)
        {
            Console.Error.WriteLine($"Process \"{inputName}\" was not found.");
            return CommandResult.ExecutionError;
        }

        var killedCount = 0;
        var exitCode = CommandResult.Success;
        var skippedCurrentProcess = false;

        foreach (var process in processes)
        {
            using (process)
            {
                var pid = TryGetProcessId(process);
                if (pid == 0)
                {
                    exitCode = exitCode == CommandResult.AccessDenied ? exitCode : CommandResult.ExecutionError;
                    continue;
                }

                if (pid == Environment.ProcessId)
                {
                    skippedCurrentProcess = true;
                    continue;
                }

                switch (Terminate(process))
                {
                    case TerminationResult.Killed:
                        killedCount++;
                        break;
                    case TerminationResult.AccessDenied:
                        exitCode = CommandResult.AccessDenied;
                        break;
                    case TerminationResult.NotFound:
                        exitCode = exitCode == CommandResult.AccessDenied ? exitCode : CommandResult.ExecutionError;
                        break;
                    default:
                        exitCode = exitCode == CommandResult.AccessDenied ? exitCode : CommandResult.ExecutionError;
                        break;
                }
            }
        }

        if (skippedCurrentProcess)
        {
            Console.Error.WriteLine("Cannot kill the current ViYuki process.");
        }

        if (killedCount > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"{killedCount} process{(killedCount == 1 ? string.Empty : "es")} killed.");
        }

        return killedCount == 0 && exitCode == CommandResult.Success
            ? CommandResult.ExecutionError
            : exitCode;
    }

    private static TerminationResult Terminate(Process process)
    {
        int pid;
        string processName;

        try
        {
            pid = process.Id;
            processName = process.ProcessName;
            process.Kill(entireProcessTree: true);

            if (!process.WaitForExit(ExitTimeoutMilliseconds))
            {
                Console.Error.WriteLine($"Process PID {pid} did not exit within {ExitTimeoutMilliseconds / 1000} seconds.");
                return TerminationResult.Failed;
            }

            Console.WriteLine($"Killed {FormatProcessName(processName)} (PID {pid})");
            return TerminationResult.Killed;
        }
        catch (UnauthorizedAccessException)
        {
            pid = TryGetProcessId(process);
            PrintAccessDeniedMessage(pid);
            return TerminationResult.AccessDenied;
        }
        catch (InvalidOperationException)
        {
            pid = TryGetProcessId(process);
            if (pid > 0)
            {
                Console.Error.WriteLine($"Process with PID {pid} was not found.");
            }

            return TerminationResult.NotFound;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 5)
        {
            pid = TryGetProcessId(process);
            PrintAccessDeniedMessage(pid);
            return TerminationResult.AccessDenied;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            pid = TryGetProcessId(process);
            Console.Error.WriteLine($"kill: PID {pid}: {ex.Message}");
            return TerminationResult.Failed;
        }
    }

    private static int InvalidSyntax()
    {
        PrintUsage();
        return CommandResult.InvalidArguments;
    }

    private static int PrintNotFound(int pid)
    {
        Console.Error.WriteLine($"Process with PID {pid} was not found.");
        return CommandResult.ExecutionError;
    }

    private static int PrintAccessDenied(int pid)
    {
        PrintAccessDeniedMessage(pid);
        return CommandResult.AccessDenied;
    }

    private static void PrintAccessDeniedMessage(int pid)
    {
        Console.Error.WriteLine($"Access denied: PID {pid}");
        Console.Error.WriteLine("Try running ViYuki as Administrator.");
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  kill -n process-name");
        Console.Error.WriteLine("  kill -id PID");
        Console.Error.WriteLine();
        Console.Error.WriteLine("Examples:");
        Console.Error.WriteLine("  kill -n notepad.exe");
        Console.Error.WriteLine("  kill -n notepad");
        Console.Error.WriteLine("  kill -id 1234");
    }

    private static string? NormalizeProcessName(string inputName)
    {
        var fileName = Path.GetFileName(inputName.Trim());
        if (fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^4];
        }

        return string.IsNullOrWhiteSpace(fileName) ? null : fileName;
    }

    private static int TryGetProcessId(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    private static string FormatProcessName(string processName)
    {
        return processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : $"{processName}.exe";
    }

    private enum TerminationResult
    {
        Killed,
        NotFound,
        AccessDenied,
        Failed
    }
}
