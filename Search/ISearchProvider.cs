namespace ViYuki.Search;

public interface ISearchProvider
{
    IAsyncEnumerable<SearchResult> SearchAsync(
        SearchRequest request,
        SearchProgress progress,
        CancellationToken cancellationToken);
}
