using ViYuki.Core;
using ViYuki.SystemIntegration;

namespace ViYuki.Commands;

public sealed class SysInfoCommand : ICommand
{
    private readonly SystemInfoService _systemInfo = new();

    public string Name => "sysinfo";

    public string Description => "Shows detailed Windows system information.";

    public string Usage => "sysinfo";

    public int Execute(CommandContext context, IReadOnlyList<string> args)
    {
        if (args.Count > 0)
        {
            Console.Error.WriteLine("Usage: sysinfo");
            return CommandResult.InvalidArguments;
        }

        _systemInfo.WriteReport();
        return CommandResult.Success;
    }
}
