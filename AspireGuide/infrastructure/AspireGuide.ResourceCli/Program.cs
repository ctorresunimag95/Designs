using AspireGuide.ResourceCli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("aspireguide");
    config.AddCommand<AddCommand>("add")
        .WithDescription("Add Aspire resources to an AppHost.");
    config.AddCommand<ListCommand>("list")
        .WithDescription("List available resources.");
    config.AddCommand<InitCommand>("init")
        .WithDescription("Scaffold a new AppHost, then run add.");
});

return app.Run(args);
