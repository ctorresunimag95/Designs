using System.Text;

namespace AspireTemplate.ResourceCli.Editing;

public sealed record FileEncodingInfo(Encoding Encoding, string LineEnding, bool HasFinalNewline);
public sealed record AppHostEdit(int LineNumber, string Content, string ResourceKey);
public sealed record PackageEdit(string ProjectPath, string PackageId, string Version, bool IsCentralManagement);
public sealed record MsbuildMetadataEdit(string ProjectPath, string ItemType, string Include, string CopyToOutputDirectory);
public sealed record FileCopyOperation(string SourcePath, string DestinationPath, bool Required, string? Namespace = null);
public sealed record SolutionEdit(string SolutionPath, string ProjectPath, string ProjectName);
public sealed record ProjectReferenceEdit(string ProjectPath, string ReferencePath);

public sealed record ChangeSet(
    IReadOnlyList<AppHostEdit> AppHostEdits,
    IReadOnlyList<PackageEdit> PackageEdits,
    IReadOnlyList<MsbuildMetadataEdit> MsbuildEdits,
    IReadOnlyList<FileCopyOperation> FileCopies,
    IReadOnlyList<SolutionEdit> SolutionEdits,
    FileEncodingInfo? EncodingInfo = null)
{
    public IReadOnlyList<ProjectReferenceEdit> ProjectReferenceEdits { get; init; } = [];

    public static ChangeSet Empty(FileEncodingInfo? encodingInfo = null) => new([], [], [], [], [], encodingInfo);
    public bool HasChanges => AppHostEdits.Count > 0 || PackageEdits.Count > 0 || MsbuildEdits.Count > 0 || FileCopies.Count > 0 || SolutionEdits.Count > 0 || ProjectReferenceEdits.Count > 0;
    public ChangeSet Merge(ChangeSet other) => new(
        AppHostEdits.Concat(other.AppHostEdits).ToArray(), PackageEdits.Concat(other.PackageEdits).ToArray(),
        MsbuildEdits.Concat(other.MsbuildEdits).ToArray(), FileCopies.Concat(other.FileCopies).ToArray(),
        SolutionEdits.Concat(other.SolutionEdits).ToArray(), EncodingInfo ?? other.EncodingInfo)
    {
        ProjectReferenceEdits = ProjectReferenceEdits.Concat(other.ProjectReferenceEdits).ToArray(),
    };
}

    public sealed record EditResult(string Text, ChangeSet Changes);

