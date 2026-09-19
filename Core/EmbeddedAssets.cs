using System.Reflection;

namespace ViYuki.Core;

public static class EmbeddedAssets
{
    public static bool TryReadText(string assetName, out string content)
    {
        var assembly = typeof(EmbeddedAssets).Assembly;
        var resourceName = $"{assembly.GetName().Name}.Assets.{assetName}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            content = string.Empty;
            return false;
        }

        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        content = reader.ReadToEnd();
        return true;
    }
}
