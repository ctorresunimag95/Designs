using System.Text;
using System.Text.RegularExpressions;
using AspireTemplate.ResourceCli.Catalog;
using AspireTemplate.ResourceCli.Templates;

namespace AspireTemplate.ResourceCli.Editing;

public sealed partial class AppHostEditor
{
    private static readonly Regex StableMarker = StableMarkerRegex();
    private static readonly Regex Region = RegionRegex();

    internal EditResult InsertRegions(string text, IEnumerable<ResourceDefinition> resources, int buildAnchorLine)
    {
        var selected = resources
            .OrderBy(r => Array.IndexOf(ResourceCatalog.InsertionOrder.ToArray(), r.Key))
            .Where(r => !IsAlreadyPresent(text, r.Key, ResourceCatalog.LegacyRegionNames.GetValueOrDefault(r.Key)))
            .ToArray();
        var metadata = PreserveFileMetadata(text);
        if (selected.Length == 0)
            return new EditResult(text, ChangeSet.Empty(metadata));

        var lines = text.Replace("\r\n", "\n").Split('\n').Select(x => x.TrimEnd('\r')).ToArray();
        if (buildAnchorLine < 1 || buildAnchorLine > lines.Length)
            throw new ArgumentOutOfRangeException(nameof(buildAnchorLine));

        var insertion = string.Join(metadata.LineEnding, selected.Select(r => TemplateLoader.LoadRegion(r.Key).TrimEnd('\r', '\n')));
        var anchorIndex = buildAnchorLine - 1;
        var before = string.Join(metadata.LineEnding, lines.Take(anchorIndex));
        var after = string.Join(metadata.LineEnding, lines.Skip(anchorIndex));
        var result = (before.Length == 0 ? string.Empty : before + metadata.LineEnding) + insertion + metadata.LineEnding + after;
        if (!metadata.HasFinalNewline) result = result.TrimEnd('\r', '\n');
        else if (!result.EndsWith(metadata.LineEnding, StringComparison.Ordinal)) result += metadata.LineEnding;

        var edits = selected.Select(r => new AppHostEdit(buildAnchorLine, TemplateLoader.LoadRegion(r.Key), r.Key)).ToArray();
        return new EditResult(result, new ChangeSet(edits, [], [], [], [], metadata));
    }

    public bool IsAlreadyPresent(string text, string resourceKey, string? legacyRegionName = null) =>
        DetectStableMarker(text, resourceKey) || (!string.IsNullOrWhiteSpace(legacyRegionName) && DetectLegacyRegion(text, legacyRegionName));

    internal static bool DetectStableMarker(string text, string resourceKey) =>
        StableMarker.Matches(text).Any(m => string.Equals(m.Groups["key"].Value, resourceKey, StringComparison.OrdinalIgnoreCase));

    internal static bool DetectLegacyRegion(string text, string legacyRegionName) =>
        Region.Matches(text).Any(m => string.Equals(m.Groups["name"].Value.Trim(), legacyRegionName, StringComparison.OrdinalIgnoreCase));

    internal static string DeduplicateUsings(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return string.Join("\n", text.Replace("\r\n", "\n").Split('\n').Where(line =>
        {
            var trimmed = line.Trim();
            return !trimmed.StartsWith("using ", StringComparison.Ordinal) || seen.Add(trimmed);
        }));
    }

    internal static bool ValidateProjectReferences(string text, IEnumerable<ResourceDefinition> selected) =>
        !selected.Any(r => r.Key.Equals("sbexplorer", StringComparison.OrdinalIgnoreCase)) ||
        ServiceBusExplorerRegex().IsMatch(text);

    internal static FileEncodingInfo PreserveFileMetadata(string text)
    {
        var encoding = text.Length > 0 && text[0] == '\ufeff' ? new UTF8Encoding(true) : new UTF8Encoding(false);
        var lineEnding = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        return new FileEncodingInfo(encoding, lineEnding, text.EndsWith('\n'));
    }

    [GeneratedRegex(@"#\s*region\s+(?<name>[^\r\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled, "es-CO")]
    private static partial Regex RegionRegex();

    [GeneratedRegex(@"Projects\.AspireTemplate_ServiceBusExplorer\b", RegexOptions.CultureInvariant)]
    private static partial Regex ServiceBusExplorerRegex();
    [GeneratedRegex("//\\s*<AspireTemplate\\s+resource\\s*=\\s*[\\\"'](?<key>[^\\\"']+)[\\\"']\\s*>", RegexOptions.IgnoreCase | RegexOptions.Compiled, "es-CO")]
    private static partial Regex StableMarkerRegex();
}