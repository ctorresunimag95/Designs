using AspireGuide.ResourceCli.Templates;
using Xunit;

namespace AspireGuide.ResourceCli.Tests.Templates;

public class TemplateLoaderTests
{
    private static readonly string[] ResourceKeys =
        ["servicebus", "blobstorage", "appconfiguration", "keycloak", "sbexplorer"];

    [Theory]
    [InlineData("servicebus")]
    [InlineData("blobstorage")]
    [InlineData("appconfiguration")]
    [InlineData("keycloak")]
    [InlineData("sbexplorer")]
    public void RegionTemplate_Should_ReturnContent(string key)
    {
        var content = TemplateLoader.LoadRegion(key);
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Theory]
    [InlineData("servicebus")]
    [InlineData("blobstorage")]
    [InlineData("appconfiguration")]
    [InlineData("keycloak")]
    [InlineData("sbexplorer")]
    public void RegionTemplate_Should_ContainOpeningMarker(string key)
    {
        var content = TemplateLoader.LoadRegion(key);
        Assert.Contains($"// <aspireguide resource=\"{key}\">", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("servicebus")]
    [InlineData("blobstorage")]
    [InlineData("appconfiguration")]
    [InlineData("keycloak")]
    [InlineData("sbexplorer")]
    public void RegionTemplate_Should_ContainClosingMarker(string key)
    {
        var content = TemplateLoader.LoadRegion(key);
        Assert.Contains("// </aspireguide>", content, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("servicebus", "Service Bus")]
    [InlineData("blobstorage", "Blob Storage")]
    [InlineData("appconfiguration", "App Configuration")]
    [InlineData("keycloak", "Identity")]
    [InlineData("sbexplorer", "Service Bus Explorer")]
    public void RegionTemplate_Should_ContainRegionTitle(string key, string regionTitle)
    {
        var content = TemplateLoader.LoadRegion(key);
        Assert.Contains($"region {regionTitle}", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RegionTemplate_Should_ThrowForUnknownKey()
    {
        Assert.Throws<InvalidOperationException>(() => TemplateLoader.LoadRegion("notaresource"));
    }

    [Theory]
    [InlineData("Extensions/ServiceBusExtensions.cs")]
    [InlineData("Extensions/BlobExtensions.cs")]
    [InlineData("Extensions/KeycloakExtensions.cs")]
    [InlineData("Keycloak/realm-export.json")]
    [InlineData("Content/SampleFiles/Sample.txt")]
    public void CompanionFile_Should_ReturnContent(string path)
    {
        var content = TemplateLoader.ReadCompanionFile(path);
        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Fact]
    public void RealmExportJson_Should_BeValidJson()
    {
        Assert.True(TemplateLoader.IsValidJson("Keycloak/realm-export.json"));
    }

    [Fact]
    public void RealmExportJson_Should_ContainAspireGuideRealm()
    {
        var content = TemplateLoader.ReadCompanionFile("Keycloak/realm-export.json");
        Assert.Contains("aspire-guide", content, StringComparison.Ordinal);
    }

    [Fact]
    public void AllResourceNames_Should_IncludeAllRegionTemplates()
    {
        var names = TemplateLoader.GetAllResourceNames();
        foreach (var key in ResourceKeys)
            Assert.Contains(names, n => n.EndsWith($".regions.{key}.txt", StringComparison.Ordinal));
    }
}
