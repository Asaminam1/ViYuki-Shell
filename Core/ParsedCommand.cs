namespace ViYuki.Core;

public sealed record ParsedCommand(string Name, IReadOnlyList<string> Arguments)
{
    public static ParsedCommand Empty { get; } = new(string.Empty, Array.Empty<string>());

    public bool IsEmpty => string.IsNullOrWhiteSpace(Name);
}
