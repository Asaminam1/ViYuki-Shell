using ViYuki.Core;
using ViYuki.SystemIntegration;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;

namespace ViYuki.Commands;

public sealed class VersionCommand : ICommand
{
    private readonly GitHubUpdateService _updateService;

    public VersionCommand(GitHubUpdateService updateService)
    {
        _updateService = updateService;
    }

    public string Name => "version";

    public string Description => "Shows the ViYuki Shell version.";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            Console.Error.WriteLine("version: this command does not accept arguments");
            return CommandResult.InvalidArguments;
        }

        if (!EmbeddedAssets.TryReadText("version.txt", out var template))
        {
            Console.Error.WriteLine("version: display template is unavailable");
            return CommandResult.ExecutionError;
        }

        RenderFastfetch(context, template);
        ShowUpdateNotice();
        return CommandResult.Success;
    }

    private void ShowUpdateNotice()
    {
        UpdateRelease? update;
        try
        {
            update = _updateService.TryGetUpdateAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return;
        }

        if (update is null)
        {
            return;
        }

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"A new ViYuki Shell version {update.Version} is available. Run -update to install it.");
        Console.ResetColor();
    }

    private static void RenderFastfetch(CommandContext context, string template)
    {
        var logoLines = ExtractLogoLines(template);
        var infoLines = BuildInfoLines(context);
        var logoWidth = logoLines.Count == 0 ? 0 : logoLines.Max(line => line.Length);
        var infoWidth = infoLines.Max(line => line.Length);
        var gap = 4;
        var previousColor = Console.ForegroundColor;

        if (GetConsoleWidth() >= logoWidth + gap + infoWidth)
        {
            RenderSideBySide(logoLines, infoLines, logoWidth, infoWidth, gap);
        }
        else
        {
            RenderStacked(logoLines, infoLines, logoWidth, infoWidth);
        }

        Console.ForegroundColor = previousColor;
    }

    private static void RenderSideBySide(
        IReadOnlyList<string> logoLines,
        IReadOnlyList<string> infoLines,
        int logoWidth,
        int infoWidth,
        int gap)
    {
        var infoOffset = Math.Max(0, (logoLines.Count - infoLines.Count) / 2);
        var lineCount = Math.Max(logoLines.Count, infoOffset + infoLines.Count);
        var combinedWidth = logoWidth + gap + infoWidth;
        var leftMargin = Math.Max(0, (GetConsoleWidth() - combinedWidth) / 2);

        for (var index = 0; index < lineCount; index++)
        {
            var logoLine = index < logoLines.Count ? logoLines[index] : string.Empty;
            var infoIndex = index - infoOffset;

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(new string(' ', leftMargin));
            Console.Write(logoLine);

            if (infoIndex >= 0 && infoIndex < infoLines.Count)
            {
                Console.Write(new string(' ', logoWidth - logoLine.Length + gap));
                Console.ForegroundColor = infoIndex == 0 ? ConsoleColor.Cyan : ConsoleColor.White;
                Console.WriteLine(infoLines[infoIndex]);
            }
            else
            {
                Console.WriteLine();
            }
        }
    }

    private static void RenderStacked(
        IReadOnlyList<string> logoLines,
        IReadOnlyList<string> infoLines,
        int logoWidth,
        int infoWidth)
    {
        var logoMargin = Math.Max(0, (GetConsoleWidth() - logoWidth) / 2);
        foreach (var logoLine in logoLines)
        {
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write(new string(' ', logoMargin));
            Console.WriteLine(logoLine);
        }

        Console.WriteLine();
        var infoMargin = Math.Max(0, (GetConsoleWidth() - infoWidth) / 2);
        for (var index = 0; index < infoLines.Count; index++)
        {
            Console.ForegroundColor = index == 0 ? ConsoleColor.Cyan : ConsoleColor.White;
            Console.Write(new string(' ', infoMargin));
            Console.WriteLine(infoLines[index]);
        }
    }

    private static int GetConsoleWidth()
    {
        try
        {
            return Console.WindowWidth;
        }
        catch (IOException)
        {
            return 120;
        }
    }

    private static List<string> ExtractLogoLines(string template)
    {
        var lines = template.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        var infoStart = lines.FindIndex(line => line.StartsWith("ViYuki Shell", StringComparison.OrdinalIgnoreCase));
        if (infoStart < 0)
        {
            throw new InvalidDataException("Version display template does not contain the info section.");
        }

        var logoLines = lines.Take(infoStart).ToList();
        while (logoLines.Count > 0 && string.IsNullOrWhiteSpace(logoLines[^1]))
        {
            logoLines.RemoveAt(logoLines.Count - 1);
        }

        return logoLines;
    }

    private static IReadOnlyList<string> BuildInfoLines(CommandContext context)
    {
        var drive = new DriveInfo(Path.GetPathRoot(context.CurrentDirectory)!);
        var processor = OperatingSystem.IsWindows() ? GetProcessorName() : "unavailable";
        var graphicsAdapter = OperatingSystem.IsWindows() ? GetGraphicsAdapterName() : "unavailable";
        var usedPercent = drive.TotalSize == 0
            ? 0
            : (int)Math.Round((1d - (double)drive.AvailableFreeSpace / drive.TotalSize) * 100);

        return
        [
            $"{AppInfo.Name} {AppInfo.Version}",
            new string('─', 24),
            $"OS       {RuntimeInformation.OSDescription.Trim()}",
            "Shell    CMD / PowerShell",
            $"Terminal {AppInfo.Name}",
            $"Directory {context.CurrentDirectory}",
            $"Time     {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"CPU      {processor}",
            $"GPU      {graphicsAdapter}",
            $"Memory   {GetMemoryInfo()}",
            $"Disk     {FormatBytes((ulong)(drive.TotalSize - drive.AvailableFreeSpace))} / {FormatBytes((ulong)drive.TotalSize)} ({usedPercent}%)"
        ];
    }

    private static string GetMemoryInfo()
    {
        var status = new MemoryStatusEx
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>()
        };

        return GlobalMemoryStatusEx(ref status)
            ? $"{FormatBytes(status.AvailablePhysical)} free / {FormatBytes(status.TotalPhysical)}"
            : "unavailable";
    }

    [SupportedOSPlatform("windows")]
    private static string GetProcessorName()
    {
        return ReadRegistryString(
                   Registry.LocalMachine,
                   @"HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                   "ProcessorNameString")?.Trim()
               ?? $"{Environment.ProcessorCount} logical processors";
    }

    [SupportedOSPlatform("windows")]
    private static string GetGraphicsAdapterName()
    {
        try
        {
            using var videoKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Video");
            if (videoKey is null)
            {
                return "unavailable";
            }

            foreach (var adapterId in videoKey.GetSubKeyNames())
            {
                var name = ReadRegistryString(
                    videoKey,
                    $@"{adapterId}\0000",
                    "DriverDesc");
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains("Microsoft Basic Display", StringComparison.OrdinalIgnoreCase))
                {
                    return name.Trim();
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
        }

        return "unavailable";
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadRegistryString(RegistryKey root, string path, string valueName)
    {
        try
        {
            using var key = root.OpenSubKey(path);
            return key?.GetValue(valueName) as string;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string FormatBytes(ulong bytes)
    {
        const double gigabyte = 1024d * 1024d * 1024d;
        return $"{bytes / gigabyte:0.0} GB";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);
}
