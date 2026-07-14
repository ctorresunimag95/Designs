namespace AspireGuide.ResourceCli.Catalog;

public sealed record ResourceDefinition(
    string Key,
    string DisplayName,
    string Description,
    ResourceType Type,
    /// <summary>Name used in #region / # region markers in the AppHost.</summary>
    string RegionTitle,
    /// <summary>Keys of resources that must be present before this one can be added.</summary>
    string[] Dependencies,
    PackageRequirement[] Packages,
    CompanionFile[] CompanionFiles,
    ProjectRequirement? RequiredProject = null);
