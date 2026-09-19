namespace ViYuki.Core;

public sealed class CommandParseException : Exception
{
    public CommandParseException(string message)
        : base(message)
    {
    }
}
