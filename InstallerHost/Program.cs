using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

const string PayloadMagic = "VYUKI-SFX-1";
var exitCode = Install();
return exitCode;

static int Install()
{
    try
    {
        var installerPath = Environment.ProcessPath
            ?? throw new InvalidOperationException("Cannot determine installer path.");
        var installDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ViYuki");
        var temporaryZip = ExtractPayload(installerPath);

        try
        {
            Directory.CreateDirectory(installDirectory);
            ExtractPayloadToInstallDirectory(temporaryZip, installDirectory);
            RemoveObsoleteAssets(installDirectory);
            MigrateLegacyConfig(installDirectory);
            AddToUserPath(installDirectory);
            CreateStartMenuShortcut(installDirectory);

            Console.WriteLine($"ViYuki installed to {installDirectory}");
            Console.WriteLine("Open a new terminal window, then run: viyuki");
            Console.WriteLine("For an already open CMD, run: %LOCALAPPDATA%\\ViYuki\\Enable-ViYuki.cmd");
            return 0;
        }
        finally
        {
            File.Delete(temporaryZip);
        }
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"ViYuki setup failed: {ex.Message}");
        return 1;
    }
}

static string ExtractPayload(string installerPath)
{
    var magic = Encoding.ASCII.GetBytes(PayloadMagic);
    var footerLength = magic.Length + sizeof(long);

    using var source = new FileStream(installerPath, FileMode.Open, FileAccess.Read, FileShare.Read);
    if (source.Length < footerLength)
    {
        throw new InvalidDataException("Installer payload is missing.");
    }

    source.Position = source.Length - footerLength;
    var footerMagic = new byte[magic.Length];
    source.ReadExactly(footerMagic);
    if (!footerMagic.SequenceEqual(magic))
    {
        throw new InvalidDataException("Installer payload signature is invalid.");
    }

    using var reader = new BinaryReader(source, Encoding.UTF8, leaveOpen: true);
    var payloadOffset = reader.ReadInt64();
    var payloadLength = source.Length - footerLength - payloadOffset;
    if (payloadOffset < 0 || payloadLength <= 0)
    {
        throw new InvalidDataException("Installer payload boundaries are invalid.");
    }

    source.Position = payloadOffset;
    var temporaryZip = Path.Combine(Path.GetTempPath(), $"ViYuki-{Guid.NewGuid():N}.zip");
    using var destination = new FileStream(temporaryZip, FileMode.CreateNew, FileAccess.Write, FileShare.None);
    source.CopyTo(destination);

    destination.SetLength(payloadLength);
    return temporaryZip;
}

static void RemoveObsoleteAssets(string installDirectory)
{
    var assetsDirectory = Path.Combine(installDirectory, "Assets");
    if (Directory.Exists(assetsDirectory))
    {
        Directory.Delete(assetsDirectory, recursive: true);
    }
}

static void MigrateLegacyConfig(string installDirectory)
{
    var legacyConfigPath = Path.Combine(installDirectory, "config.json");
    if (!File.Exists(legacyConfigPath))
    {
        return;
    }

    var configDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ViYukiShell");
    var configPath = Path.Combine(configDirectory, "config.json");
    Directory.CreateDirectory(configDirectory);

    if (!File.Exists(configPath))
    {
        File.Copy(legacyConfigPath, configPath);
    }

    File.Delete(legacyConfigPath);
}

static void ExtractPayloadToInstallDirectory(string zipPath, string installDirectory)
{
    using var archive = ZipFile.OpenRead(zipPath);
    foreach (var entry in archive.Entries)
    {
        var targetPath = Path.GetFullPath(Path.Combine(installDirectory, entry.FullName));
        if (!targetPath.StartsWith(installDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("Installer payload contains an invalid path.");
        }

        if (string.IsNullOrEmpty(entry.Name))
        {
            Directory.CreateDirectory(targetPath);
            continue;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
        try
        {
            entry.ExtractToFile(targetPath, overwrite: true);
        }
        catch (Exception ex) when (
            entry.Name.Equals("Enable-ViYuki.cmd", StringComparison.OrdinalIgnoreCase) &&
            ex is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine("Existing Enable-ViYuki.cmd is in use; keeping it.");
        }
    }
}

static void AddToUserPath(string installDirectory)
{
    var userPath = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? string.Empty;
    var parts = userPath.Split(';', StringSplitOptions.RemoveEmptyEntries);
    if (!parts.Any(part => string.Equals(part.TrimEnd('\\'), installDirectory, StringComparison.OrdinalIgnoreCase)))
    {
        var newPath = string.IsNullOrWhiteSpace(userPath)
            ? installDirectory
            : $"{userPath.TrimEnd(';')};{installDirectory}";
        Environment.SetEnvironmentVariable("Path", newPath, EnvironmentVariableTarget.User);
    }

    NotifyEnvironmentChanged();
}

static void CreateStartMenuShortcut(string installDirectory)
{
    var startMenu = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Microsoft", "Windows", "Start Menu", "Programs");
    Directory.CreateDirectory(startMenu);

    var shellType = Type.GetTypeFromProgID("WScript.Shell");
    if (shellType is null)
    {
        return;
    }

    dynamic shell = Activator.CreateInstance(shellType)!;
    dynamic shortcut = shell.CreateShortcut(Path.Combine(startMenu, "ViYuki.lnk"));
    shortcut.TargetPath = Path.Combine(installDirectory, "ViYuki.exe");
    shortcut.WorkingDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    shortcut.Save();
}

static void NotifyEnvironmentChanged()
{
    _ = SendMessageTimeout(
        new IntPtr(0xffff),
        0x001a,
        UIntPtr.Zero,
        "Environment",
        0x0002,
        5000,
        out _);
}

[DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
static extern IntPtr SendMessageTimeout(
    IntPtr hWnd,
    uint message,
    UIntPtr wParam,
    string lParam,
    uint flags,
    uint timeout,
    out UIntPtr result);
