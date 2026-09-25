namespace ViYuki.Search;

public sealed record SearchRequest(
    string Query,
    IReadOnlyList<string> Roots,
    int MaxDegreeOfParallelism = 4);
