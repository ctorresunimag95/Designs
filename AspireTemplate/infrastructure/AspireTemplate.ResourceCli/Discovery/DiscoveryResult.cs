namespace AspireTemplate.ResourceCli.Discovery;

public abstract record DiscoveryResult;

/// <summary>A single AppHost project was found and the build anchor was located.</summary>
public sealed record AppHostFound(
    string ProjectPath,
    string ProgramFilePath,
    int BuildAnchorLine) : DiscoveryResult;

/// <summary>More than one AppHost candidate was found — the user must choose.</summary>
public sealed record MultipleAppHostsFound(
    IReadOnlyList<string> Candidates) : DiscoveryResult;

/// <summary>No AppHost was found in the search scope.</summary>
public sealed record NoAppHostFound : DiscoveryResult;

/// <summary>An AppHost was found but it has no recognisable build anchor.</summary>
public sealed record BuildAnchorMissing(
    string ProjectPath,
    string ProgramFilePath) : DiscoveryResult;
