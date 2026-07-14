namespace AspireGuide.ResourceCli.Commands;

internal static class ExitCodes
{
    /// <summary>Success, nothing to do, dry-run, or user-aborted.</summary>
    internal const int Success = 0;

    /// <summary>Apply failed mid-write; all changes were rolled back.</summary>
    internal const int ApplyFailed = 1;

    /// <summary>Discovery, validation, or configuration error.</summary>
    internal const int Error = 2;
}
