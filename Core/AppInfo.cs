using System.Reflection;

namespace ViYuki.Core;

public static class AppInfo
{
    public const string Name = "ViYuki Shell";
    public const string FallbackVersion = "1.16";
    public const string RepositoryOwner = "Asaminam1";
    public const string RepositoryName = "ViYuki-Shell";
    public const string SetupAssetName = "ViYukiSetup.exe";

    public static string Version => GetVersion();

    private static string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly();
        var informationalVersion = assembly?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion?
            .Split('+', 2)[0]
            .Trim();

        return global::System.Version.TryParse(informationalVersion, out _)
            ? informationalVersion
            : FallbackVersion;
    }
}
