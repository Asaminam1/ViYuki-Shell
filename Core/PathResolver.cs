namespace ViYuki.Core;

public static class PathResolver
{
    public static string Resolve(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.CurrentDirectory;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path);
        return Path.GetFullPath(expanded, Environment.CurrentDirectory);
    }
}
