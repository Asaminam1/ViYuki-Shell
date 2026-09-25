namespace ViYuki.Core;

public static class CommandResult
{
    public const int Success = 0;
    public const int ExecutionError = 1;
    public const int InvalidArguments = 2;
    public const int AccessDenied = 3;
    public const int Cancelled = 130;
    public const int CommandNotFound = 127;
}
