using System.Diagnostics;
using System.Text.Json;
using ViYuki.Core;
using ViYuki.SystemIntegration;

namespace ViYuki.Commands;

public sealed class UpdateCommand : ICommand
{
    private readonly GitHubUpdateService _updateService;

    public UpdateCommand(GitHubUpdateService updateService)
    {
        _updateService = updateService;
    }

    public string Name => "-update";

    public string Description => "Downloads and installs the latest ViYuki Shell release.";

    public string Usage => "-update";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            Console.Error.WriteLine("Usage: -update");
            return CommandResult.InvalidArguments;
        }

        try
        {
            var update = _updateService.TryGetUpdateAsync().GetAwaiter().GetResult();
            if (update is null)
            {
                Console.WriteLine("ViYuki Shell is already up to date, or no public release is available yet.");
                return CommandResult.Success;
            }

            Console.WriteLine($"Downloading ViYuki Shell {update.Version}...");
            var setupPath = _updateService.DownloadInstallerAsync(update).GetAwaiter().GetResult();
            StartInstallerAfterExit(setupPath);
            Console.WriteLine("The installer will start in a moment.");
            context.RequestExit();
            return CommandResult.Success;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or JsonException)
        {
            Console.Error.WriteLine($"update: {ex.Message}");
            return CommandResult.ExecutionError;
        }
    }

    private static void StartInstallerAfterExit(string setupPath)
    {
        var escapedPath = setupPath.Replace("\"", "\"\"");
        var command = $"ping 127.0.0.1 -n 3 > nul & start \"\" \"{escapedPath}\"";
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /c {command}",
            WorkingDirectory = Path.GetTempPath(),
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            throw new IOException("Unable to start the downloaded installer.");
        }
    }
}
