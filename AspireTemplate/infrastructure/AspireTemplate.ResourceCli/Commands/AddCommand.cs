using System.ComponentModel;
using AspireTemplate.ResourceCli.Catalog;
using AspireTemplate.ResourceCli.Discovery;
using AspireTemplate.ResourceCli.Editing;
using AspireTemplate.ResourceCli.Templates;
using AspireTemplate.ResourceCli.Ux;
using AspireTemplate.ResourceCli.Validation;
using Spectre.Console;
using Spectre.Console.Cli;

namespace AspireTemplate.ResourceCli.Commands;

internal sealed class AddCommand : Command<AddCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--resource <KEY>")]
        [Description("Resource key(s) to add (repeatable).")]
        public string[]? Resources { get; init; }

        [CommandOption("--all")]
        [Description("Select all compatible resources.")]
        public bool All { get; init; }

        [CommandOption("--yes|-y")]
        [Description("Skip confirmation prompts.")]
        public bool Yes { get; init; }

        [CommandOption("--dry-run")]
        [Description("Print planned changes without writing anything.")]
        public bool DryRun { get; init; }

        [CommandOption("--root <PATH>")]
        [Description("Path to the repository root or AppHost project.")]
        public string? Root { get; init; }
    }

    public override int Execute(CommandContext context, Settings settings) =>
        Run(settings, new SpectreConsoleUx());

    /// <summary>
    /// Core logic, injectable for testing and for use from InitCommand.
    /// </summary>
    internal static int Run(Settings settings, IConsoleUx ux, AppHostLocator? locatorOverride = null)
    {
        // ── 1. Discovery ──────────────────────────────────────────────────────
        var locator = locatorOverride ?? new AppHostLocator();
        DiscoveryResult discovery;
        try
        {
            discovery = locator.Locate(settings.Root);
        }
        catch (Exception ex)
        {
            ux.ShowError("Discovery failed.", ex);
            return ExitCodes.Error;
        }

        if (discovery is not AppHostFound found)
        {
            var earlyIssues = new PreflightValidator().Validate(discovery, [], settings.Root ?? Directory.GetCurrentDirectory());
            ux.ShowIssues(earlyIssues);
            return ExitCodes.Error;
        }

        ux.ShowAppHostFound(found.ProjectPath);

        // ── 2. Read AppHost to detect already-installed resources ─────────────
        string appHostText;
        try { appHostText = File.ReadAllText(found.ProgramFilePath); }
        catch (Exception ex) { ux.ShowError("Cannot read AppHost file.", ex); return ExitCodes.Error; }

        var editor = new AppHostEditor();
        var alreadyInstalled = ResourceCatalog.All
            .Where(r => editor.IsAlreadyPresent(appHostText, r.Key, ResourceCatalog.LegacyRegionNames.GetValueOrDefault(r.Key)))
            .Select(r => r.Key)
            .ToArray();

        // ── 3. Resolve requested resources ───────────────────────────────────
        ResourceDefinition[] requested;
        if (settings.Resources is { Length: > 0 })
        {
            var resolved = ResolveByKeys(settings.Resources, ux);
            if (resolved is null) return ExitCodes.Error;
            requested = resolved;
        }
        else if (settings.All)
        {
            requested = ResourceCatalog.All.ToArray();
        }
        else
        {
            // Interactive multi-select
            var selected = ux.PromptResourceSelection(ResourceCatalog.All, alreadyInstalled);
            if (selected is null or { Count: 0 })
            {
                ux.WriteInfo("No resources selected — nothing to do.");
                return ExitCodes.Success;
            }
            requested = [.. selected];
        }

        // ── 4. Expand transitive dependencies in deterministic order ──────────
        var byKey = requested.ToDictionary(r => r.Key, StringComparer.OrdinalIgnoreCase);
        foreach (var resource in requested.ToArray())
        {
            foreach (var dep in resource.Dependencies)
            {
                if (!byKey.ContainsKey(dep) && ResourceCatalog.Find(dep) is { } depDef)
                    byKey[depDef.Key] = depDef;
            }
        }
        requested = ResourceCatalog.InsertionOrder
            .Select(k => byKey.GetValueOrDefault(k))
            .Where(r => r is not null)
            .Cast<ResourceDefinition>()
            .ToArray();

        // ── 5. Validation ─────────────────────────────────────────────────────
        var projectDirectory = Path.GetDirectoryName(found.ProjectPath)!;
        var validator = new PreflightValidator();
        var issues = validator.Validate(discovery, requested, projectDirectory);
        ux.ShowIssues(issues);
        if (issues.Any(i => i.IsError)) return ExitCodes.Error;

        // ── 6. Compute ChangeSet ──────────────────────────────────────────────
        var repoRoot = RepositoryLocator.FindRoot(projectDirectory);
        var rootNamespace = Discovery.AppHostLocator.DetectRootNamespace(found.ProjectPath);
        var projectText = File.ReadAllText(found.ProjectPath);
        var appHostResult = editor.InsertRegions(appHostText, requested, found.BuildAnchorLine);
        var csprojEditor = new CsprojEditor();
        var packageResult = csprojEditor.AddPackageReferences(projectText, requested.SelectMany(r => r.Packages), found.ProjectPath);
        var changes = appHostResult.Changes.Merge(packageResult.Changes);

        string updatedAppHostText = appHostResult.Text;
        string updatedProjectText = packageResult.Text;
        string? pendingSolutionPath = null;
        string? updatedSolutionText = null;

        foreach (var resource in requested)
        {
            foreach (var companion in resource.CompanionFiles)
            {
                var dest = Path.Combine(projectDirectory, companion.DestinationPath.Replace('/', Path.DirectorySeparatorChar));
                var ns = companion.SourcePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                    ? rootNamespace + ".Extensions"
                    : null;
                changes = changes with
                {
                    FileCopies = changes.FileCopies.Append(new FileCopyOperation(companion.SourcePath, dest, companion.Required, ns)).ToArray()
                };
            }
            if (resource.Key.Equals("keycloak", StringComparison.OrdinalIgnoreCase))
            {
                var metaResult = csprojEditor.AddContentMetadata(updatedProjectText, "Content", "Keycloak\\**", found.ProjectPath);
                updatedProjectText = metaResult.Text;
                changes = changes.Merge(metaResult.Changes);
            }
            if (resource.Key.Equals("blobstorage", StringComparison.OrdinalIgnoreCase))
            {
                var metaResult = csprojEditor.AddContentMetadata(updatedProjectText, "Content", "Content\\**", found.ProjectPath);
                updatedProjectText = metaResult.Text;
                changes = changes.Merge(metaResult.Changes);
            }

            if (resource.RequiredProject is { RelativePath: not null } req)
            {
                var absProjectPath = Path.GetFullPath(Path.Combine(repoRoot, req.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
                var relRef = Path.GetRelativePath(projectDirectory, absProjectPath);
                var projRefResult = csprojEditor.AddProjectReference(updatedProjectText, relRef, found.ProjectPath);
                updatedProjectText = projRefResult.Text;
                changes = changes.Merge(projRefResult.Changes);

                if (!File.Exists(absProjectPath) && req.ScaffoldFiles is { Count: > 0 })
                {
                    var scaffoldDir = Path.GetDirectoryName(absProjectPath)!;
                    foreach (var scaffoldFile in req.ScaffoldFiles)
                    {
                        var sourcePath = $"projects/{resource.Key}/{scaffoldFile.Replace('\\', '/')}";
                        var destPath = Path.Combine(scaffoldDir, scaffoldFile.Replace('/', Path.DirectorySeparatorChar));
                        changes = changes with
                        {
                            FileCopies = changes.FileCopies.Append(new FileCopyOperation(sourcePath, destPath, Required: false)).ToArray(),
                        };
                    }

                    var slnFiles = Directory.GetFiles(repoRoot, "*.sln")
                        .Concat(Directory.GetFiles(repoRoot, "*.slnx"))
                        .ToArray();
                    if (slnFiles.Length == 1)
                    {
                        var slnPath = slnFiles[0];
                        var slnContent = File.ReadAllText(slnPath);
                        var relInSolution = Path.GetRelativePath(Path.GetDirectoryName(slnPath)!, absProjectPath);
                        var projectName = Path.GetFileNameWithoutExtension(absProjectPath);
                        var slnResult = new SolutionEditor().AddProjectReference(slnContent, relInSolution, projectName, slnPath);
                        if (slnResult.Changes.HasChanges)
                        {
                            pendingSolutionPath = slnPath;
                            updatedSolutionText = slnResult.Text;
                            changes = changes.Merge(slnResult.Changes);
                        }
                    }
                }
            }
        }

        // ── 7. Dry-run ────────────────────────────────────────────────────────
        ux.ShowPlan(changes, found.ProgramFilePath, found.ProjectPath);

        if (settings.DryRun)
        {
            ux.WriteInfo("Dry-run — no files written.");
            return ExitCodes.Success;
        }

        if (!changes.HasChanges)
        {
            ux.WriteInfo("All selected resources are already present — nothing to do.");
            return ExitCodes.Success;
        }

        // ── 8. Confirmation ───────────────────────────────────────────────────
        if (!settings.Yes && !ux.Confirm("Apply these changes?"))
        {
            ux.WriteInfo("Aborted.");
            return ExitCodes.Success;
        }

        // ── 9. Transactional apply ────────────────────────────────────────────
        var encoding = appHostResult.Changes.EncodingInfo?.Encoding ?? System.Text.Encoding.UTF8;
        Exception? applyError = null;

        using var writer = new FileWriter();
        try
        {
            ux.ShowProgress("Applying changes…", () =>
            {
                writer.WriteText(found.ProgramFilePath, updatedAppHostText, encoding);
                writer.WriteText(found.ProjectPath, updatedProjectText, encoding);

                if (pendingSolutionPath is not null && updatedSolutionText is not null)
                    writer.WriteText(pendingSolutionPath, updatedSolutionText, encoding);

                foreach (var copy in changes.FileCopies)
                    writer.CopyCompanion(copy);
            });
            writer.Commit();
        }
        catch (Exception ex)
        {
            applyError = ex;
            // writer.Dispose() will rollback via the using block
        }

        if (applyError is not null)
        {
            ux.ShowError("Apply failed — all changes have been rolled back.", applyError);
            return ExitCodes.ApplyFailed;
        }

        ux.ShowSuccess($"Done! Added {changes.AppHostEdits.Count} resource(s) to {Path.GetFileName(found.ProgramFilePath)}.");
        return ExitCodes.Success;
    }

    private static ResourceDefinition[]? ResolveByKeys(IEnumerable<string> keys, IConsoleUx ux)
    {
        var result = new List<ResourceDefinition>();
        foreach (var key in keys)
        {
            var resource = ResourceCatalog.Find(key);
            if (resource is null) { ux.ShowError($"Unknown resource key: '{key}'. Run 'AspireTemplate list' to see available resources."); return null; }
            if (result.All(r => !r.Key.Equals(resource.Key, StringComparison.OrdinalIgnoreCase))) result.Add(resource);
        }
        return [.. result];
    }
}
