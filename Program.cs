using ViYuki.Commands;
using ViYuki.Config;
using ViYuki.Core;
using ViYuki.SystemIntegration;
using System.Text;

Console.OutputEncoding = Encoding.UTF8;
var config = AppConfig.LoadOrCreateDefault();
var parser = new CommandParser();
var registry = new CommandRegistry(config.Aliases);
var updateService = new GitHubUpdateService();

registry.Register(new HelpCommand(registry));
registry.Register(new VersionCommand(updateService));
registry.Register(new UpdateCommand(updateService));
registry.Register(new ExitCommand());
registry.Register(new ClearCommand());
registry.Register(new CdCommand());
registry.Register(new LsCommand());
registry.Register(new RmCommand());

var externalExecutor = new ExternalCommandExecutor();
var shell = new Shell(registry, parser, externalExecutor);

Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shell.RequestExit();
};

if (args.Length == 0)
{
    return shell.RunInteractive();
}

return shell.RunCommand(args);
