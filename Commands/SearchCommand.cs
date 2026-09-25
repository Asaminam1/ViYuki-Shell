using System.Diagnostics;
using System.Globalization;
using ViYuki.Core;
using ViYuki.Search;

namespace ViYuki.Commands;

public sealed class SearchCommand : ICommand
{
    private readonly ISearchProvider _searchProvider;

    public SearchCommand(ISearchProvider searchProvider)
    {
        _searchProvider = searchProvider;
    }

    public string Name => "search";

    public string Description => "Fast recursive file search.";

    public string Usage => "search query\n  search query path\n  search query -disk drive [-disk drive ...]\n  search query -everywhere\n\nExamples:\n  search name\n  search name.txt\n  search .txt\n  search name C:\\Windows\n  search name.txt \"C:\\Program Files\"\n  search name -disk d\n  search name -disk d -disk h\n  search name -everywhere";

    public bool SupportsCancellation => true;

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        var parsed = ParseArguments(context, args);
        if (parsed.Request is null)
        {
            Console.Error.WriteLine(parsed.Error);
            Console.Error.WriteLine($"Usage: {Usage}");
            return CommandResult.InvalidArguments;
        }

        return ExecuteSearchAsync(parsed.Request, context.CancellationToken).GetAwaiter().GetResult();
    }

    private async Task<int> ExecuteSearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        var progress = new SearchProgress();
        var stopwatch = Stopwatch.StartNew();
        var matchCount = 0L;

        try
        {
            await foreach (var result in _searchProvider.SearchAsync(request, progress, cancellationToken))
            {
                Console.WriteLine(result.FullPath);
                matchCount++;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            Console.WriteLine("Search cancelled.");
            if (progress.SkippedDirectories > 0)
            {
                Console.WriteLine($"Skipped inaccessible directories: {progress.SkippedDirectories}");
            }

            PrintScanStatistics(progress, stopwatch.Elapsed);
            return CommandResult.Cancelled;
        }

        stopwatch.Stop();
        Console.WriteLine();

        if (matchCount == 0)
        {
            Console.WriteLine($"No files matching \"{request.Query}\" were found.");
        }
        else
        {
            Console.WriteLine($"Found: {matchCount} {(matchCount == 1 ? "file" : "files")}");
        }

        if (request.Roots.Count > 1)
        {
            Console.WriteLine($"Searched: {string.Join(", ", request.Roots)}");
        }

        if (progress.SkippedDirectories > 0 || request.Roots.Count > 1)
        {
            Console.WriteLine($"Skipped inaccessible directories: {progress.SkippedDirectories}");
        }

        PrintScanStatistics(progress, stopwatch.Elapsed);
        return CommandResult.Success;
    }

    private static ParsedSearchArguments ParseArguments(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            return ParsedSearchArguments.Invalid("search: missing search query");
        }

        var query = args[0];
        var remaining = args.Skip(1).ToArray();
        if (remaining.Length == 0)
        {
            return ParsedSearchArguments.Valid(new SearchRequest(query, [context.CurrentDirectory]));
        }

        var hasEverywhere = remaining.Any(argument => IsOption(argument, "-everywhere"));
        var hasDisk = remaining.Any(argument => IsOption(argument, "-disk"));

        if (hasEverywhere)
        {
            if (hasDisk)
            {
                return ParsedSearchArguments.Invalid("search: -everywhere cannot be combined with -disk");
            }

            if (remaining.Length != 1 || !IsOption(remaining[0], "-everywhere"))
            {
                return ParsedSearchArguments.Invalid("search: -everywhere cannot be combined with a path or other arguments");
            }

            var roots = GetAvailableLocalDrives();
            return roots.Count == 0
                ? ParsedSearchArguments.Invalid("search: no available local drives were found")
                : ParsedSearchArguments.Valid(new SearchRequest(query, roots));
        }

        if (hasDisk)
        {
            var roots = new List<string>();
            for (var index = 0; index < remaining.Length; index += 2)
            {
                if (index + 1 >= remaining.Length || !IsOption(remaining[index], "-disk"))
                {
                    return ParsedSearchArguments.Invalid("search: expected pairs of -disk drive");
                }

                if (!TryNormalizeAvailableDrive(remaining[index + 1], out var root))
                {
                    return ParsedSearchArguments.Invalid($"search: drive '{remaining[index + 1]}' is unavailable or invalid");
                }

                if (!roots.Contains(root, StringComparer.OrdinalIgnoreCase))
                {
                    roots.Add(root);
                }
            }

            return ParsedSearchArguments.Valid(new SearchRequest(query, roots));
        }

        if (remaining.Length != 1)
        {
            return ParsedSearchArguments.Invalid("search: specify one directory, one or more -disk options, or -everywhere");
        }

        if (remaining[0].StartsWith("-", StringComparison.Ordinal))
        {
            return ParsedSearchArguments.Invalid($"search: unknown option '{remaining[0]}'");
        }

        var directory = PathResolver.Resolve(remaining[0]);
        if (!Directory.Exists(directory))
        {
            return ParsedSearchArguments.Invalid($"search: directory does not exist: {remaining[0]}");
        }

        return ParsedSearchArguments.Valid(new SearchRequest(query, [directory]));
    }

    private static IReadOnlyList<string> GetAvailableLocalDrives()
    {
        var roots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable or DriveType.Ram)
                {
                    roots.Add(drive.RootDirectory.FullName);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return roots;
    }

    private static bool TryNormalizeAvailableDrive(string input, out string root)
    {
        root = string.Empty;
        var value = input.Trim().TrimEnd('\\', '/');
        if (value.Length == 2 && value[1] == ':')
        {
            value = value[..1];
        }

        if (value.Length != 1 || !char.IsAsciiLetter(value[0]))
        {
            return false;
        }

        var normalizedRoot = $"{char.ToUpperInvariant(value[0])}:\\";
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.IsReady && drive.RootDirectory.FullName.Equals(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    root = normalizedRoot;
                    return true;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return false;
    }

    private static bool IsOption(string argument, string option)
    {
        return argument.Equals(option, StringComparison.OrdinalIgnoreCase);
    }

    private static void PrintScanStatistics(SearchProgress progress, TimeSpan elapsed)
    {
        var fileLabel = progress.FilesScanned == 1 ? "file" : "files";
        var directoryLabel = progress.DirectoriesScanned == 1 ? "directory" : "directories";
        Console.WriteLine($"Scanned: {progress.FilesScanned} {fileLabel}, {progress.DirectoriesScanned} {directoryLabel}");
        Console.WriteLine($"Time: {elapsed.TotalSeconds.ToString("0.00", CultureInfo.InvariantCulture)} s");
    }

    private sealed record ParsedSearchArguments(SearchRequest? Request, string? Error)
    {
        public static ParsedSearchArguments Valid(SearchRequest request) => new(request, null);

        public static ParsedSearchArguments Invalid(string error) => new(null, error);
    }
}
