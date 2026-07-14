using AspireGuide.ResourceCli.Catalog;
using AspireGuide.ResourceCli.Editing;
using AspireGuide.ResourceCli.Validation;

namespace AspireGuide.ResourceCli.Ux;

public interface IConsoleUx
{
    /// <summary>Shows an AppHost discovery status panel.</summary>
    void ShowAppHostFound(string appHostPath);

    /// <summary>
    /// Interactive multi-select for resources.
    /// <paramref name="alreadyInstalledKeys"/> resources are shown checked and disabled.
    /// Returns null if the user cancels or nothing is selected.
    /// </summary>
    IReadOnlyList<ResourceDefinition>? PromptResourceSelection(
        IReadOnlyList<ResourceDefinition> available,
        IReadOnlyList<string> alreadyInstalledKeys);

    /// <summary>Renders the planned ChangeSet as a table.</summary>
    void ShowPlan(ChangeSet changes, string appHostPath, string projectPath);

    /// <summary>Asks the user to confirm the plan. Returns true to proceed.</summary>
    bool Confirm(string prompt);

    /// <summary>Runs <paramref name="work"/> while showing a progress spinner.</summary>
    void ShowProgress(string title, Action work);

    void ShowSuccess(string message);
    void ShowError(string message, Exception? ex = null);
    void WriteWarning(string message);
    void WriteInfo(string message);

    /// <summary>Renders validation issues (errors in red, warnings in yellow).</summary>
    void ShowIssues(IEnumerable<ValidationIssue> issues);
}
