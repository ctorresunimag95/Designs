using AspireGuide.ResourceCli.Catalog;
using AspireGuide.ResourceCli.Editing;
using AspireGuide.ResourceCli.Validation;

namespace AspireGuide.ResourceCli.Ux;

/// <summary>
/// Non-interactive console UX for use in tests and non-interactive pipelines.
/// Auto-confirms and selects all non-disabled resources.
/// </summary>
public sealed class NullConsoleUx : IConsoleUx
{
    private readonly bool _autoConfirm;
    private readonly IReadOnlyList<ResourceDefinition>? _selectionOverride;

    public List<string> Output { get; } = [];

    public NullConsoleUx(bool autoConfirm = true, IReadOnlyList<ResourceDefinition>? selectionOverride = null)
    {
        _autoConfirm = autoConfirm;
        _selectionOverride = selectionOverride;
    }

    public void ShowAppHostFound(string appHostPath) =>
        Output.Add($"AppHostFound: {appHostPath}");

    public IReadOnlyList<ResourceDefinition>? PromptResourceSelection(
        IReadOnlyList<ResourceDefinition> available,
        IReadOnlyList<string> alreadyInstalledKeys)
    {
        if (_selectionOverride is not null) return _selectionOverride;

        var installed = new HashSet<string>(alreadyInstalledKeys, StringComparer.OrdinalIgnoreCase);
        return available.Where(r => !installed.Contains(r.Key)).ToArray();
    }

    public void ShowPlan(ChangeSet changes, string appHostPath, string projectPath) =>
        Output.Add($"Plan: {changes.AppHostEdits.Count} edits, {changes.PackageEdits.Count} packages, {changes.FileCopies.Count} files");

    public bool Confirm(string prompt) => _autoConfirm;

    public void ShowProgress(string title, Action work) => work();

    public void ShowSuccess(string message) => Output.Add($"Success: {message}");

    public void ShowError(string message, Exception? ex = null) =>
        Output.Add($"Error: {message}{(ex is null ? "" : $" ({ex.Message})")}");

    public void WriteWarning(string message) => Output.Add($"Warning: {message}");

    public void WriteInfo(string message) => Output.Add($"Info: {message}");

    public void ShowIssues(IEnumerable<ValidationIssue> issues)
    {
        foreach (var i in issues) Output.Add($"{i.Severity} {i.Code}: {i.Message}");
    }
}
