using System.Text.RegularExpressions;

namespace AspireTemplate.ResourceCli.Editing;

public sealed class SolutionEditor
{
    public EditResult AddProjectReference(string content, string projectPath, string projectName, string solutionPath = "")
    {
        if (IsProjectPresent(content, projectPath)) return new EditResult(content, ChangeSet.Empty());
        var newline = content.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        string line;
        if (content.TrimStart().StartsWith("<Solution", StringComparison.OrdinalIgnoreCase))
        {
            var tag = $"  <Project Path=\"{projectPath.Replace('\\', '/')}\" />{newline}";
            line = content.Replace("</Solution>", tag + "</Solution>", StringComparison.OrdinalIgnoreCase);
        }
        else
        {
            line = content.TrimEnd('\r', '\n') + newline;
            line += $"Project(\"{{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}}\") = \"{projectName}\", \"{projectPath.Replace('/', '\\')}\", \"{{{Guid.NewGuid().ToString().ToUpperInvariant()}}}\"{newline}EndProject" + newline;
        }
        return new EditResult(line, new ChangeSet([], [], [], [], [new SolutionEdit(solutionPath, projectPath, projectName)], null));
    }

    internal bool IsProjectPresent(string content, string projectPath)
    {
        var normalized = projectPath.Replace('\\', '/');
        return content.Split('\n').Any(line =>
            line.Replace('\\', '/').Contains(normalized, StringComparison.OrdinalIgnoreCase));
    }
}