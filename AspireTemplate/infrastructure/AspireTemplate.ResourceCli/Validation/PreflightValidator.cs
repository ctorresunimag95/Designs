using System.Xml.Linq;
using AspireTemplate.ResourceCli.Catalog;
using AspireTemplate.ResourceCli.Discovery;
using AspireTemplate.ResourceCli.Editing;
using AspireTemplate.ResourceCli.Templates;

namespace AspireTemplate.ResourceCli.Validation;

public sealed class PreflightValidator
{
    internal List<ValidationIssue> Validate(DiscoveryResult discovery, IEnumerable<ResourceDefinition> selectedResources, string appHostDir)
    {
        var selected = selectedResources.ToArray();
        var issues = new List<ValidationIssue>();
        switch (discovery)
        {
            case BuildAnchorMissing:
                issues.Add(new("MISSING_BUILD_ANCHOR", "The AppHost has no builder.Build().Run() or RunAsync() anchor.", IssueSeverity.Error));
                break;
            case MultipleAppHostsFound:
                issues.Add(new("MULTIPLE_APPHOSTS", "Multiple AppHosts were found; select one explicitly.", IssueSeverity.Error));
                break;
            case NoAppHostFound:
                issues.Add(new("NO_APPHOST", "No Aspire AppHost was found. Run init or provide --root.", IssueSeverity.Error));
                break;
            case not AppHostFound:
                issues.Add(new("INVALID_DISCOVERY", "The discovery result is not a usable AppHost.", IssueSeverity.Error));
                break;
        }

        issues.AddRange(ValidateDependencies(selected));
        issues.AddRange(ValidateProjectPrerequisites(selected, appHostDir));
        if (discovery is AppHostFound found)
        {
            issues.AddRange(ValidatePackageConflicts(found.ProjectPath, selected));
            issues.AddRange(ValidateCompanionFileConflicts(Path.GetDirectoryName(found.ProjectPath)!, selected));
            issues.AddRange(ValidateWritable(found.ProjectPath, found.ProgramFilePath));
        }
        return issues;
    }

    internal List<ValidationIssue> ValidateDependencies(IEnumerable<ResourceDefinition> selected)
    {
        var keys = selected.Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return selected.SelectMany(resource => resource.Dependencies.Where(d => !keys.Contains(d)).Select(d =>
            new ValidationIssue("MISSING_DEPENDENCY", $"Resource '{resource.Key}' requires resource '{d}'.", IssueSeverity.Error))).ToList();
    }

    internal List<ValidationIssue> ValidateProjectPrerequisites(IEnumerable<ResourceDefinition> selected, string appHostDir)
    {
        var issues = new List<ValidationIssue>();
        var root = FindRepositoryRoot(appHostDir);
        foreach (var resource in selected)
        {
            if (resource.RequiredProject is not { } requirement) continue;
            var path = requirement.RelativePath is null ? null : Path.Combine(root, requirement.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            if (path is null || !File.Exists(path))
                issues.Add(new("MISSING_REQUIRED_PROJECT", $"Resource '{resource.Key}' requires project '{requirement.ProjectName}', which was not found and will be scaffolded.", IssueSeverity.Warning));
        }
        return issues;
    }

    internal List<ValidationIssue> ValidatePackageConflicts(string appHostCsprojPath, IEnumerable<ResourceDefinition> selected)
    {
        var issues = new List<ValidationIssue>();
        if (!File.Exists(appHostCsprojPath)) return [new("MISSING_CSPROJ", $"AppHost project '{appHostCsprojPath}' does not exist.", IssueSeverity.Error)];
        var xml = File.ReadAllText(appHostCsprojPath);
        var packageElements = XDocument.Parse(xml).Descendants().Where(e => e.Name.LocalName == "PackageReference");
        foreach (var package in selected.SelectMany(r => r.Packages))
        {
            var existing = packageElements.FirstOrDefault(e => string.Equals((string?)e.Attribute("Include"), package.Id, StringComparison.OrdinalIgnoreCase));
            var version = (string?)existing?.Attribute("Version");
            if (version is not null && Version.TryParse(version, out var installed) && Version.TryParse(package.Version, out var requested) && installed < requested)
                issues.Add(new("PACKAGE_CONFLICT", $"Package '{package.Id}' is version {version}, below requested {package.Version}; it will not be downgraded automatically.", IssueSeverity.Error));
        }
        return issues;
    }

    internal List<ValidationIssue> ValidateCompanionFileConflicts(string appHostDir, IEnumerable<ResourceDefinition> selected)
    {
        var issues = new List<ValidationIssue>();
        foreach (var file in selected.SelectMany(r => r.CompanionFiles))
        {
            var destination = Path.Combine(appHostDir, file.DestinationPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(destination)) continue;
            var embedded = TemplateLoader.ReadCompanionFile(file.SourcePath);
            if (!string.Equals(File.ReadAllText(destination), embedded, StringComparison.Ordinal))
                issues.Add(new(file.Required ? "REQUIRED_COMPANION_CONFLICT" : "OPTIONAL_COMPANION_CONFLICT",
                    $"Companion file '{file.DestinationPath}' already exists with different content.", file.Required ? IssueSeverity.Error : IssueSeverity.Warning));
        }
        return issues;
    }

    internal List<ValidationIssue> ValidateWritable(params string[] filePaths)
    {
        var issues = new List<ValidationIssue>();
        foreach (var path in filePaths.Where(File.Exists))
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                issues.Add(new("TARGET_NOT_WRITABLE", $"Target '{path}' is not writable: {ex.Message}", IssueSeverity.Error));
            }
        }
        return issues;
    }

    private static string FindRepositoryRoot(string path)
    {
        var current = new DirectoryInfo(Path.GetFullPath(path));
        while (current.Parent is not null && !current.EnumerateFiles("*.slnx").Any() && !current.EnumerateFiles("*.sln").Any()) current = current.Parent;
        return current.FullName;
    }
}