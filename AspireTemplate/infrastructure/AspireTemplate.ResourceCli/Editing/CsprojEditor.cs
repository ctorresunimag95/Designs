using System.Text.RegularExpressions;
using AspireTemplate.ResourceCli.Catalog;

namespace AspireTemplate.ResourceCli.Editing;

public sealed class CsprojEditor
{
    private static readonly Regex PackageRegex = new("<PackageReference\\s+Include\\s*=\\s*[\\\"'](?<id>[^\\\"']+)[\\\"'](?<attrs>[^>]*)/?>", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex VersionRegex = new("Version\\s*=\\s*[\\\"'](?<version>[^\\\"']+)[\\\"']", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal EditResult AddPackageReferences(string xml, IEnumerable<PackageRequirement> packages, string projectPath = "")
    {
        var result = xml;
        var edits = new List<PackageEdit>();
        var central = DetectCentralPackageManagement(xml);
        foreach (var package in packages)
        {
            if (IsPackageAlreadyPresent(result, package.Id).exists) continue;
            var tag = central ? $"    <PackageReference Include=\"{package.Id}\" />" : $"    <PackageReference Include=\"{package.Id}\" Version=\"{package.Version}\" />";
            result = InsertIntoItemGroup(result, tag);
            edits.Add(new PackageEdit(projectPath, package.Id, package.Version, central));
        }
        return new EditResult(result, new ChangeSet([], edits, [], [], [], null));
    }

    internal static (bool exists, string? version) IsPackageAlreadyPresent(string xml, string packageId)
    {
        foreach (Match match in PackageRegex.Matches(xml))
        {
            if (!string.Equals(match.Groups["id"].Value, packageId, StringComparison.OrdinalIgnoreCase)) continue;
            var version = VersionRegex.Match(match.Groups["attrs"].Value).Groups["version"].Value;
            return (true, string.IsNullOrEmpty(version) ? null : version);
        }
        return (false, null);
    }

    public EditResult AddContentMetadata(string xml, string itemType, string include, string projectPath = "")
    {
        if (Regex.IsMatch(xml, $"<{Regex.Escape(itemType)}\\b[^>]*Include\\s*=\\s*[\\\"']{Regex.Escape(include)}[\\\"']", RegexOptions.IgnoreCase))
            return new EditResult(xml, ChangeSet.Empty());
        var tag = $"    <{itemType} Include=\"{include}\" CopyToOutputDirectory=\"PreserveNewest\" />";
        return new EditResult(InsertIntoItemGroup(xml, tag), new ChangeSet([], [], [new MsbuildMetadataEdit(projectPath, itemType, include, "PreserveNewest")], [], [], null));
    }

    internal EditResult AddProjectReference(string xml, string relativeRefPath, string projectPath = "")
    {
        var normalized = relativeRefPath.Replace('\\', '/');
        if (Regex.IsMatch(xml, $"<ProjectReference\\b[^>]*Include\\s*=\\s*[\\\"'][^\\\"']*{Regex.Escape(Path.GetFileName(normalized))}[\\\"']", RegexOptions.IgnoreCase))
            return new EditResult(xml, ChangeSet.Empty());
        var tag = $"    <ProjectReference Include=\"{relativeRefPath}\" />";
        var result = InsertIntoItemGroup(xml, tag);
        return new EditResult(result, new ChangeSet([], [], [], [], []) { ProjectReferenceEdits = [new ProjectReferenceEdit(projectPath, relativeRefPath)] });
    }

    internal static bool DetectCentralPackageManagement(string xml) => xml.Contains("ManagePackageVersionsCentrally", StringComparison.OrdinalIgnoreCase) || xml.Contains("<PackageVersion", StringComparison.OrdinalIgnoreCase);

    private static string InsertIntoItemGroup(string xml, string tag)
    {
        var close = xml.IndexOf("</ItemGroup>", StringComparison.OrdinalIgnoreCase);
        if (close >= 0) return xml.Insert(close, tag + Environment.NewLine);
        var projectClose = xml.LastIndexOf("</Project>", StringComparison.OrdinalIgnoreCase);
        if (projectClose < 0) throw new InvalidOperationException("Project XML does not contain </Project>.");
        return xml.Insert(projectClose, $"  <ItemGroup>{Environment.NewLine}{tag}{Environment.NewLine}  </ItemGroup>{Environment.NewLine}");
    }
}