using AspireTemplate.ResourceCli.Catalog;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AspireTemplate.ResourceCli.Commands;

internal sealed class ListCommand : Command<ListCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override int Execute(CommandContext context, Settings settings)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .Title("[bold]AspireTemplate Resource Catalog[/]")
            .AddColumn(new TableColumn("[bold]Key[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Display Name[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Type[/]").NoWrap())
            .AddColumn(new TableColumn("[bold]Packages[/]"))
            .AddColumn(new TableColumn("[bold]Requires[/]"))
            .AddColumn(new TableColumn("[bold]Description[/]"));

        foreach (var resource in ResourceCatalog.All)
        {
            var typeMarkup = resource.Type switch
            {
                ResourceType.Infrastructure => "[blue]Infrastructure[/]",
                ResourceType.Integration    => "[green]Integration[/]",
                _                           => resource.Type.ToString(),
            };

            var packages = resource.Packages.Length == 0
                ? "[grey](none)[/]"
                : string.Join("\n", resource.Packages.Select(p => $"[dim]{Markup.Escape(p.Id)}[/]\n[grey]{Markup.Escape(p.Version)}[/]"));

            var requires = BuildRequires(resource);

            table.AddRow(
                $"[yellow]{Markup.Escape(resource.Key)}[/]",
                Markup.Escape(resource.DisplayName),
                typeMarkup,
                packages,
                requires,
                Markup.Escape(resource.Description));
        }

        AnsiConsole.Write(table);
        return 0;
    }

    private static string BuildRequires(ResourceDefinition resource)
    {
        var parts = new List<string>();

        foreach (var dep in resource.Dependencies)
            parts.Add($"[yellow]{Markup.Escape(dep)}[/] [grey](resource)[/]");

        if (resource.RequiredProject is { } proj)
            parts.Add($"[cyan]{Markup.Escape(proj.ProjectName)}[/] [grey](project)[/]");

        return parts.Count == 0 ? "[grey]—[/]" : string.Join("\n", parts);
    }
}
