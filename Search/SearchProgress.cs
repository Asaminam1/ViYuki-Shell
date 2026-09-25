namespace ViYuki.Search;

public sealed class SearchProgress
{
    private long _filesScanned;
    private long _directoriesScanned;
    private long _skippedDirectories;

    public long FilesScanned => Interlocked.Read(ref _filesScanned);

    public long DirectoriesScanned => Interlocked.Read(ref _directoriesScanned);

    public long SkippedDirectories => Interlocked.Read(ref _skippedDirectories);

    internal void FileScanned() => Interlocked.Increment(ref _filesScanned);

    internal void DirectoryScanned() => Interlocked.Increment(ref _directoriesScanned);

    internal void DirectorySkipped() => Interlocked.Increment(ref _skippedDirectories);
}
