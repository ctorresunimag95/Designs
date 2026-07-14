using AspireGuide.ResourceCli.Catalog;
using AspireGuide.ResourceCli.Editing;
using Xunit;

namespace AspireGuide.ResourceCli.Tests.Editing;

public class AppHostEditorTests
{
    private static readonly AppHostEditor Editor = new();

    private const string MinimalAppHost = """
        var builder = DistributedApplication.CreateBuilder(args);

        builder.Build().Run();
        """;

    private const string CrlfAppHost = "var builder = DistributedApplication.CreateBuilder(args);\r\n\r\nbuilder.Build().Run();\r\n";

    // ── InsertRegions ─────────────────────────────────────────────────────────

    [Fact]
    public void InsertRegions_Should_InsertServiceBusBeforeBuildAnchor()
    {
        var result = Editor.InsertRegions(MinimalAppHost, [ResourceCatalog.Find("servicebus")!], buildAnchorLine: 3);
        Assert.Contains("aspireguide resource=\"servicebus\"", result.Text);
        Assert.True(result.Changes.AppHostEdits.Count > 0);
    }

    [Fact]
    public void InsertRegions_Should_PlaceInsertionBeforeAnchorLine()
    {
        var result = Editor.InsertRegions(MinimalAppHost, [ResourceCatalog.Find("appconfiguration")!], buildAnchorLine: 3);
        var anchorIndex = result.Text.IndexOf("builder.Build().Run()", StringComparison.Ordinal);
        var markerIndex = result.Text.IndexOf("aspireguide resource=\"appconfiguration\"", StringComparison.Ordinal);
        Assert.True(markerIndex < anchorIndex, "Marker should appear before the build anchor");
    }

    [Fact]
    public void InsertRegions_Should_InsertMultipleResourcesInCatalogOrder()
    {
        var resources = new[] { ResourceCatalog.Find("keycloak")!, ResourceCatalog.Find("servicebus")! };
        var result = Editor.InsertRegions(MinimalAppHost, resources, buildAnchorLine: 3);
        var sbIdx = result.Text.IndexOf("aspireguide resource=\"servicebus\"", StringComparison.Ordinal);
        var kcIdx = result.Text.IndexOf("aspireguide resource=\"keycloak\"", StringComparison.Ordinal);
        Assert.True(sbIdx < kcIdx, "servicebus should appear before keycloak per catalog order");
    }

    [Fact]
    public void InsertRegions_Should_PreserveCrlfLineEndings()
    {
        var result = Editor.InsertRegions(CrlfAppHost, [ResourceCatalog.Find("appconfiguration")!], buildAnchorLine: 3);
        Assert.Contains("\r\n", result.Text);
    }

    [Fact]
    public void InsertRegions_Should_PreserveFinalNewline()
    {
        var withNewline = MinimalAppHost + "\n";
        var result = Editor.InsertRegions(withNewline, [ResourceCatalog.Find("servicebus")!], buildAnchorLine: 3);
        Assert.True(result.Text.EndsWith('\n'));
    }

    [Fact]
    public void InsertRegions_Should_ReturnEmptyChangesWhenAllAlreadyPresent()
    {
        var first = Editor.InsertRegions(MinimalAppHost, [ResourceCatalog.Find("servicebus")!], buildAnchorLine: 3);
        var second = Editor.InsertRegions(first.Text, [ResourceCatalog.Find("servicebus")!], buildAnchorLine: 3);
        Assert.Empty(second.Changes.AppHostEdits);
        Assert.Equal(first.Text, second.Text);
    }

    // ── IsAlreadyPresent — stable markers ────────────────────────────────────

    [Fact]
    public void IsAlreadyPresent_Should_ReturnTrueForStableMarker()
    {
        var text = """
            // <aspireguide resource="servicebus">
            // content
            // </aspireguide>
            builder.Build().Run();
            """;
        Assert.True(Editor.IsAlreadyPresent(text, "servicebus"));
    }

    [Fact]
    public void IsAlreadyPresent_Should_ReturnFalseForDifferentKey()
    {
        var text = "// <aspireguide resource=\"servicebus\">\nbuilder.Build().Run();";
        Assert.False(Editor.IsAlreadyPresent(text, "keycloak"));
    }

    // ── IsAlreadyPresent — legacy regions ────────────────────────────────────

    [Fact]
    public void IsAlreadyPresent_Should_DetectLegacyIdentityRegionForKeycloak()
    {
        var text = "var builder = DistributedApplication.CreateBuilder(args);\n# region Identity\nvar kc = builder.AddLocalKeycloak();\n# endregion\nbuilder.Build().Run();";
        Assert.True(Editor.IsAlreadyPresent(text, "keycloak", legacyRegionName: "Identity"));
    }

    [Fact]
    public void IsAlreadyPresent_Should_DetectLegacyRegionWithoutSpace()
    {
        var text = "#region Identity\n// content\n#endregion\nbuilder.Build().Run();";
        Assert.True(Editor.IsAlreadyPresent(text, "keycloak", legacyRegionName: "Identity"));
    }

    [Fact]
    public void InsertRegions_Should_SkipKeycloakWhenLegacyIdentityRegionPresent()
    {
        var text = "var builder = DistributedApplication.CreateBuilder(args);\n# region Identity\n# endregion\nbuilder.Build().Run();\n";
        var result = Editor.InsertRegions(text, [ResourceCatalog.Find("keycloak")!], buildAnchorLine: 4);
        Assert.Empty(result.Changes.AppHostEdits);
    }

    // ── DeduplicateUsings ─────────────────────────────────────────────────────

    [Fact]
    public void DeduplicateUsings_Should_RemoveDuplicateUsingDirectives()
    {
        var text = "using System;\nusing System;\nusing System.IO;\n";
        var result = AppHostEditor.DeduplicateUsings(text);
        var lines = result.Split('\n').Where(l => l.TrimStart().StartsWith("using ")).ToArray();
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void DeduplicateUsings_Should_PreserveNonUsingLines()
    {
        var text = "using System;\nvar x = 1;\nusing System;\n";
        var result = AppHostEditor.DeduplicateUsings(text);
        Assert.Contains("var x = 1;", result);
    }

    // ── PreserveFileMetadata ──────────────────────────────────────────────────

    [Fact]
    public void PreserveFileMetadata_Should_DetectCrlfLineEnding()
    {
        var meta = AppHostEditor.PreserveFileMetadata("a\r\nb\r\n");
        Assert.Equal("\r\n", meta.LineEnding);
    }

    [Fact]
    public void PreserveFileMetadata_Should_DetectLfLineEnding()
    {
        var meta = AppHostEditor.PreserveFileMetadata("a\nb\n");
        Assert.Equal("\n", meta.LineEnding);
    }

    [Fact]
    public void PreserveFileMetadata_Should_DetectFinalNewlinePresent()
    {
        var meta = AppHostEditor.PreserveFileMetadata("a\nb\n");
        Assert.True(meta.HasFinalNewline);
    }

    [Fact]
    public void PreserveFileMetadata_Should_DetectFinalNewlineAbsent()
    {
        var meta = AppHostEditor.PreserveFileMetadata("a\nb");
        Assert.False(meta.HasFinalNewline);
    }
}
