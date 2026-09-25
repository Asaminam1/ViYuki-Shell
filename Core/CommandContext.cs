namespace ViYuki.Core;

public sealed class CommandContext
{
    public CommandContext(bool isInteractive)
    {
        IsInteractive = isInteractive;
    }

    public bool IsInteractive { get; }

    public bool ShouldExit { get; private set; }

    public CancellationToken CancellationToken { get; internal set; } = CancellationToken.None;

    public string CurrentDirectory
    {
        get => Environment.CurrentDirectory;
        set => Environment.CurrentDirectory = value;
    }

    public void RequestExit()
    {
        ShouldExit = true;
    }
}
