namespace AspireGuide.ResourceCli.Catalog;

/// <param name="ProjectName">The project name as it appears in Projects.* references (e.g. AspireGuide_ServiceBusExplorer).</param>
/// <param name="RelativePath">Expected relative path of the .csproj from the solution root, if known.</param>
/// <param name="ScaffoldFiles">Project-relative file paths to scaffold from embedded templates when the project doesn't exist.</param>
public sealed record ProjectRequirement(
    string ProjectName,
    string? RelativePath = null,
    IReadOnlyList<string>? ScaffoldFiles = null);
