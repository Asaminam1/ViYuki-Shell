using System.Text.Json;

namespace ViYuki.Config;

public sealed class AppConfig
{
    public Dictionary<string, string> Aliases { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ll"] = "ls",
        ["cls"] = "clear"
    };

    public static string ConfigDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ViYuki");

    public static string ConfigPath => Path.Combine(ConfigDirectory, "config.json");

    public static AppConfig LoadOrCreateDefault()
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);

            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new AppConfig();
                defaultConfig.Save();
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigPath);
            var config = JsonSerializer.Deserialize<AppConfig>(json, SerializerOptions()) ?? new AppConfig();
            config.Aliases = new Dictionary<string, string>(config.Aliases, StringComparer.OrdinalIgnoreCase);
            return config;
        }
        catch (IOException)
        {
            return new AppConfig();
        }
        catch (UnauthorizedAccessException)
        {
            return new AppConfig();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(ConfigDirectory);
        File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, SerializerOptions()));
    }

    private static JsonSerializerOptions SerializerOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
