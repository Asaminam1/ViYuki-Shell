namespace ViYuki.Input;

public sealed class CommandHistory
{
    public const int MaximumEntries = 500;
    private readonly string _path;

    public CommandHistory()
    {
        _path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ViYuki",
            "history.txt");
    }

    public IReadOnlyList<string> Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return [];
            }

            return File.ReadLines(_path)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .TakeLast(MaximumEntries)
                .ToArray();
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    public void Save(IReadOnlyList<string> entries)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

            var recentEntries = entries
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .TakeLast(MaximumEntries)
                .ToArray();
            var temporaryPath = $"{_path}.{Guid.NewGuid():N}.tmp";

            try
            {
                File.WriteAllLines(temporaryPath, recentEntries);
                File.Move(temporaryPath, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
