using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using System.Security;
using System.Threading.Channels;

namespace ViYuki.Search;

public sealed class FastFileSystemSearchProvider : ISearchProvider
{
    private const int ResultBufferCapacity = 256;

    public async IAsyncEnumerable<SearchResult> SearchAsync(
        SearchRequest request,
        SearchProgress progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<SearchResult>(new BoundedChannelOptions(ResultBufferCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = request.Roots.Count == 1
        });

        var producer = Task.Run(
            () => ProduceResultsAsync(request, progress, channel.Writer, cancellationToken),
            CancellationToken.None);

        try
        {
            await foreach (var result in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return result;
            }
        }
        finally
        {
            try
            {
                await producer.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }

    private static async Task ProduceResultsAsync(
        SearchRequest request,
        SearchProgress progress,
        ChannelWriter<SearchResult> writer,
        CancellationToken cancellationToken)
    {
        Exception? failure = null;

        try
        {
            var parallelism = Math.Clamp(request.MaxDegreeOfParallelism, 1, request.Roots.Count);
            await Parallel.ForEachAsync(
                request.Roots,
                new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = parallelism
                },
                async (root, token) =>
                {
                    await ScanRootAsync(root, request.Query, progress, writer, token).ConfigureAwait(false);
                }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            failure = ex;
            throw;
        }
        finally
        {
            writer.TryComplete(failure);
        }
    }

    private static async Task ScanRootAsync(
        string root,
        string query,
        SearchProgress progress,
        ChannelWriter<SearchResult> writer,
        CancellationToken cancellationToken)
    {
        var pendingDirectories = new Stack<string>();
        pendingDirectories.Push(root);

        while (pendingDirectories.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = pendingDirectories.Pop();
            progress.DirectoryScanned();

            using var enumerator = TryCreateEnumerator(directory, progress);
            if (enumerator is null)
            {
                continue;
            }

            while (TryMoveNext(enumerator, progress, out var entry))
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.IsDirectory)
                {
                    if ((entry.Attributes & FileAttributes.ReparsePoint) == 0)
                    {
                        pendingDirectories.Push(entry.FullPath);
                    }

                    continue;
                }

                progress.FileScanned();
                if (entry.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    await writer.WriteAsync(new SearchResult(entry.FullPath), cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    private static IEnumerator<SearchEntry>? TryCreateEnumerator(string directory, SearchProgress progress)
    {
        try
        {
            var options = new EnumerationOptions
            {
                AttributesToSkip = 0,
                IgnoreInaccessible = false,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            };

            return new FileSystemEnumerable<SearchEntry>(
                directory,
                static (ref FileSystemEntry entry) => new SearchEntry(
                    entry.ToFullPath(),
                    entry.FileName.ToString(),
                    entry.IsDirectory,
                    entry.Attributes),
                options).GetEnumerator();
        }
        catch (Exception ex) when (IsSkippableFileSystemError(ex))
        {
            progress.DirectorySkipped();
            return null;
        }
    }

    private static bool TryMoveNext(
        IEnumerator<SearchEntry> enumerator,
        SearchProgress progress,
        out SearchEntry entry)
    {
        try
        {
            if (enumerator.MoveNext())
            {
                entry = enumerator.Current;
                return true;
            }
        }
        catch (Exception ex) when (IsSkippableFileSystemError(ex))
        {
            progress.DirectorySkipped();
        }

        entry = default;
        return false;
    }

    private static bool IsSkippableFileSystemError(Exception exception)
    {
        return exception is UnauthorizedAccessException
            or DirectoryNotFoundException
            or FileNotFoundException
            or IOException
            or SecurityException;
    }

    private readonly record struct SearchEntry(
        string FullPath,
        string Name,
        bool IsDirectory,
        FileAttributes Attributes);
}
