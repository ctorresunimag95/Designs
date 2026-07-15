namespace AspireTemplate.ResourceCli.Catalog;

/// <param name="SourcePath">Path within Templates/files/ embedded resources.</param>
/// <param name="DestinationPath">Relative destination path under the AppHost project directory.</param>
/// <param name="Required">True = fail on conflict; false = warn and skip.</param>
public sealed record CompanionFile(string SourcePath, string DestinationPath, bool Required);
