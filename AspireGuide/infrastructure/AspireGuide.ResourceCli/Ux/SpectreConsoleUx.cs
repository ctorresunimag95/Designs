using AspireGuide.ResourceCli.Catalog;
using AspireGuide.ResourceCli.Editing;
using AspireGuide.ResourceCli.Validation;
using Spectre.Console;

namespace AspireGuide.ResourceCli.Ux;

public sealed class SpectreConsoleUx : IConsoleUx
{
    public void ShowAppHostFound(string appHostPath)
    {
        var panel = new Panel($"[green]AppHost found:[/] {Markup.Escape(appHostPath)}")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green);
        AnsiConsole.Write(panel);
    }

    public IReadOnlyList<ResourceDefinition>? PromptResourceSelection(
        IReadOnlyList<ResourceDefinition> available,
        IReadOnlyList<string> alreadyInstalledKeys)
    {
        var installed = new HashSet<string>(alreadyInstalledKeys, StringComparer.OrdinalIgnoreCase);

        var prompt = new MultiSelectionPrompt<ResourceDefinition>()
            .Title("Select resources to add:")
            .NotRequired()
            .PageSize(10)
            .UseConverter(r =>
            {
                var suffix = installed.Contains(r.Key)
                    ? " [grey](already installed)[/]"
                    : r.Dependencies.Length > 0
                        ? $" [grey](requires: {string.Join(", ", r.Dependencies)})[/]"
                        : string.Empty;
                return $"{Markup.Escape(r.DisplayName)}{suffix}";
            });

        foreach (var resource in available)
        {
            if (installed.Contains(resource.Key))
                prompt.AddChoiceGroup(resource, []).Select(resource);
            else
                prompt.AddChoice(resource);
        }

        var selected = AnsiConsole.Prompt(prompt);

        // auto-resolve dependencies
        var byKey = available.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
        var result = selected.ToList();
        foreach (var resource in selected.ToArray())
        {
            foreach (var dep in resource.Dependencies)
            {
                if (!result.Any(r => r.Key.Equals(dep, StringComparison.OrdinalIgnoreCase))
                    && byKey.TryGetValue(dep, out var depDef))
                {
                    AnsiConsole.MarkupLine($"[yellow]Auto-adding dependency:[/] {Markup.Escape(depDef.DisplayName)}");
                    result.Add(depDef);
                }
            }
        }

        return result.Count == 0 ? null : result;
    }

    public void ShowPlan(ChangeSet changes, string appHostPath, string projectPath)
    {
        if (!changes.HasChanges)
        {
            AnsiConsole.MarkupLine("[grey]No changes planned — all selected resources are already present.[/]");
            return;
        }

        var table = new Table().Border(TableBorder.Rounded)
            .AddColumn("File").AddColumn("Type").AddColumn("Action").AddColumn("Details");

        foreach (var e in changes.AppHostEdits)
            table.AddRow(Markup.Escape(Path.GetFileName(appHostPath)), "Edit", "Add region", Markup.Escape(e.ResourceKey));
        foreach (var e in changes.PackageEdits)
            table.AddRow(Markup.Escape(Path.GetFileName(projectPath)), "Package", "Add", Markup.Escape($"{e.PackageId} {e.Version}"));
        foreach (var e in changes.MsbuildEdits)
            table.AddRow(Markup.Escape(Path.GetFileName(projectPath)), "MSBuild", "Add", Markup.Escape($"{e.ItemType} {e.Include}"));
        foreach (var c in changes.FileCopies)
            table.AddRow(Markup.Escape(Path.GetFileName(c.DestinationPath)), "File", "Copy", Markup.Escape(c.SourcePath));
        foreach (var s in changes.SolutionEdits)
            table.AddRow(Markup.Escape(Path.GetFileName(s.SolutionPath)), "Solution", "Add project", Markup.Escape(s.ProjectName));
        foreach (var p in changes.ProjectReferenceEdits)
            table.AddRow(Markup.Escape(Path.GetFileName(p.ProjectPath)), "ProjectRef", "Add", Markup.Escape(p.ReferencePath));

        AnsiConsole.Write(table);
    }

    public bool Confirm(string prompt) =>
        AnsiConsole.Confirm(prompt);

    public void ShowProgress(string title, Action work) =>
        AnsiConsole.Status().Start(title, _ => work());

    public void ShowSuccess(string message)
    {
        var panel = new Panel($"[green]{Markup.Escape(message)}[/]")
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Green);
        AnsiConsole.Write(panel);
    }

    public void ShowError(string message, Exception? ex = null)
    {
        var body = ex is null ? Markup.Escape(message) : $"{Markup.Escape(message)}\n[grey]{Markup.Escape(ex.Message)}[/]";
        var panel = new Panel(body)
            .Border(BoxBorder.Rounded)
            .BorderColor(Color.Red)
            .Header("[red]Error[/]");
        AnsiConsole.Write(panel);
    }

    public void WriteWarning(string message) =>
        AnsiConsole.MarkupLine($"[yellow]Warning:[/] {Markup.Escape(message)}");

    public void WriteInfo(string message) =>
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(message)}[/]");

    public void ShowIssues(IEnumerable<ValidationIssue> issues)
    {
        foreach (var issue in issues)
        {
            var color = issue.Severity == IssueSeverity.Error ? "red" : "yellow";
            AnsiConsole.MarkupLine($"[{color}]{issue.Severity}[/] [{color}]{Markup.Escape(issue.Code)}[/]: {Markup.Escape(issue.Message)}");
        }
    }
}
