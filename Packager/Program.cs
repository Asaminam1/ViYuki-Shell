using System.IO.Compression;
using System.Text;

const string PayloadMagic = "VYUKI-SFX-1";

var projectRoot = args.Length < 3 ? FindProjectRoot(Environment.CurrentDirectory) : string.Empty;
var appPublishDirectory = args.ElementAtOrDefault(0)
    ?? Path.Combine(projectRoot, "bin", "Release", "net8.0", "win-x64", "publish");
var setupHostPath = args.ElementAtOrDefault(1)
    ?? Path.Combine(projectRoot, "InstallerHost", "bin", "Release", "net8.0", "win-x64", "publish", "ViYukiSetupHost.exe");
var outputPath = args.ElementAtOrDefault(2)
    ?? Path.Combine(projectRoot, "artifacts", "ViYukiSetup.exe");

if (!Directory.Exists(appPublishDirectory))
{
    return Fail($"ViYuki publish directory not found: {appPublishDirectory}");
}

if (!File.Exists(setupHostPath))
{
    return Fail($"Setup host not found: {setupHostPath}");
}

try
{
    var temporaryZip = Path.Combine(Path.GetTempPath(), $"ViYuki-{Guid.NewGuid():N}.zip");
    try
    {
        CreatePayload(appPublishDirectory, temporaryZip);
        CreateSetup(setupHostPath, temporaryZip, outputPath);
    }
    finally
    {
        File.Delete(temporaryZip);
    }

    Console.WriteLine($"Created portable installer: {outputPath}");
    return 0;
}
catch (Exception ex)
{
    return Fail(ex.Message);
}

static void CreatePayload(string sourceDirectory, string zipPath)
{
    using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
    foreach (var path in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        if (Path.GetExtension(path).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        var entryName = Path.GetRelativePath(sourceDirectory, path)
            .Replace(Path.DirectorySeparatorChar, '/');
        archive.CreateEntryFromFile(path, entryName, CompressionLevel.SmallestSize);
    }
}

static void CreateSetup(string setupHostPath, string zipPath, string outputPath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
    using var output = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
    using (var host = new FileStream(setupHostPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        host.CopyTo(output);
    }

    var payloadOffset = output.Position;
    using (var payload = new FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read))
    {
        payload.CopyTo(output);
    }

    using var writer = new BinaryWriter(output, Encoding.UTF8, leaveOpen: true);
    writer.Write(Encoding.ASCII.GetBytes(PayloadMagic));
    writer.Write(payloadOffset);
}

static string FindProjectRoot(string directory)
{
    var current = new DirectoryInfo(directory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "ViYuki.csproj")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Cannot locate ViYuki.csproj. Run the packager from the project directory or provide all paths explicitly.");
}

static int Fail(string message)
{
    Console.Error.WriteLine($"ViYuki packager: {message}");
    return 1;
}
