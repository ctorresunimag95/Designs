using AspireTemplate.ResourceCli.Catalog;
using AspireTemplate.ResourceCli.Editing;
using Xunit;

namespace AspireTemplate.ResourceCli.Tests.Editing;

public class CsprojEditorTests
{
    private readonly CsprojEditor _editor = new();

    private const string MinimalCsproj = """
        <Project Sdk="Aspire.AppHost.Sdk/13.4.6">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
          </ItemGroup>
        </Project>
        """;

    private const string CentralCsproj = """
        <Project Sdk="Aspire.AppHost.Sdk/13.4.6">
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
          </ItemGroup>
        </Project>
        """;

    // ── AddPackageReferences ──────────────────────────────────────────────────

    [Fact]
    public void AddPackageReferences_Should_InsertPackageIntoItemGroup()
    {
        var packages = new[] { new PackageRequirement("Aspire.Hosting.Azure.ServiceBus", "13.4.6") };
        var result = _editor.AddPackageReferences(MinimalCsproj, packages);
        Assert.Contains("Aspire.Hosting.Azure.ServiceBus", result.Text);
        Assert.Contains("13.4.6", result.Text);
        Assert.Single(result.Changes.PackageEdits);
    }

    [Fact]
    public void AddPackageReferences_Should_BeIdempotent()
    {
        var packages = new[] { new PackageRequirement("Aspire.Hosting.Azure.ServiceBus", "13.4.6") };
        var first = _editor.AddPackageReferences(MinimalCsproj, packages);
        var second = _editor.AddPackageReferences(first.Text, packages);
        Assert.Empty(second.Changes.PackageEdits);
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public void AddPackageReferences_Should_OmitVersionForCentralPackageManagement()
    {
        var packages = new[] { new PackageRequirement("Aspire.Hosting.Azure.ServiceBus", "13.4.6") };
        var result = _editor.AddPackageReferences(CentralCsproj, packages);
        Assert.Contains("<PackageReference Include=\"Aspire.Hosting.Azure.ServiceBus\" />", result.Text);
        Assert.DoesNotContain("Version=", result.Text.Split('\n').Last(l => l.Contains("Aspire.Hosting.Azure.ServiceBus")));
    }

    [Fact]
    public void AddPackageReferences_Should_AddMultiplePackages()
    {
        var packages = new[]
        {
            new PackageRequirement("Aspire.Hosting.Azure.ServiceBus", "13.4.6"),
            new PackageRequirement("Azure.Messaging.ServiceBus", "7.20.1"),
        };
        var result = _editor.AddPackageReferences(MinimalCsproj, packages);
        Assert.Equal(2, result.Changes.PackageEdits.Count);
    }

    [Fact]
    public void AddPackageReferences_Should_SkipAlreadyPresentPackage()
    {
        var xml = MinimalCsproj.Replace("</ItemGroup>",
            "    <PackageReference Include=\"Aspire.Hosting.Azure.ServiceBus\" Version=\"13.4.6\" />\n  </ItemGroup>");
        var result = _editor.AddPackageReferences(xml, [new PackageRequirement("Aspire.Hosting.Azure.ServiceBus", "13.4.6")]);
        Assert.Empty(result.Changes.PackageEdits);
    }

    // ── IsPackageAlreadyPresent ───────────────────────────────────────────────

    [Fact]
    public void IsPackageAlreadyPresent_Should_ReturnTrueWithVersionWhenPresent()
    {
        var xml = MinimalCsproj.Replace("</ItemGroup>",
            "    <PackageReference Include=\"Foo.Bar\" Version=\"1.2.3\" />\n  </ItemGroup>");
        var (exists, version) = CsprojEditor.IsPackageAlreadyPresent(xml, "Foo.Bar");
        Assert.True(exists);
        Assert.Equal("1.2.3", version);
    }

    [Fact]
    public void IsPackageAlreadyPresent_Should_ReturnFalseWhenAbsent()
    {
        var (exists, _) = CsprojEditor.IsPackageAlreadyPresent(MinimalCsproj, "Missing.Package");
        Assert.False(exists);
    }

    [Fact]
    public void IsPackageAlreadyPresent_Should_BeCaseInsensitive()
    {
        var xml = MinimalCsproj.Replace("</ItemGroup>",
            "    <PackageReference Include=\"FOO.BAR\" Version=\"1.0.0\" />\n  </ItemGroup>");
        var (exists, _) = CsprojEditor.IsPackageAlreadyPresent(xml, "foo.bar");
        Assert.True(exists);
    }

    // ── AddContentMetadata ───────────────────────────────────────────────────

    [Fact]
    public void AddContentMetadata_Should_AddContentItemForKeycloak()
    {
        var result = _editor.AddContentMetadata(MinimalCsproj, "Content", "Keycloak\\**");
        Assert.Contains("Keycloak\\**", result.Text);
        Assert.Contains("PreserveNewest", result.Text);
        Assert.Single(result.Changes.MsbuildEdits);
    }

    [Fact]
    public void AddContentMetadata_Should_BeIdempotent()
    {
        var first = _editor.AddContentMetadata(MinimalCsproj, "Content", "Keycloak\\**");
        var second = _editor.AddContentMetadata(first.Text, "Content", "Keycloak\\**");
        Assert.Empty(second.Changes.MsbuildEdits);
    }

    // ── DetectCentralPackageManagement ───────────────────────────────────────

    [Fact]
    public void DetectCentralPackageManagement_Should_ReturnTrueForManagePackageVersionsCentrally()
    {
        Assert.True(CsprojEditor.DetectCentralPackageManagement(CentralCsproj));
    }

    [Fact]
    public void DetectCentralPackageManagement_Should_ReturnFalseForStandardProject()
    {
        Assert.False(CsprojEditor.DetectCentralPackageManagement(MinimalCsproj));
    }

    // ── AddProjectReference ──────────────────────────────────────────────────

    [Fact]
    public void AddProjectReference_Should_InsertProjectReferenceIntoItemGroup()
    {
        var result = _editor.AddProjectReference(MinimalCsproj, "../AspireTemplate.ServiceBusExplorer/AspireTemplate.ServiceBusExplorer.csproj");
        Assert.Contains("ProjectReference", result.Text);
        Assert.Contains("AspireTemplate.ServiceBusExplorer.csproj", result.Text);
        Assert.Single(result.Changes.ProjectReferenceEdits);
    }

    [Fact]
    public void AddProjectReference_Should_BeIdempotent()
    {
        var first = _editor.AddProjectReference(MinimalCsproj, "../AspireTemplate.ServiceBusExplorer/AspireTemplate.ServiceBusExplorer.csproj");
        var second = _editor.AddProjectReference(first.Text, "../AspireTemplate.ServiceBusExplorer/AspireTemplate.ServiceBusExplorer.csproj");
        Assert.Empty(second.Changes.ProjectReferenceEdits);
        Assert.Equal(first.Text, second.Text);
    }

    [Fact]
    public void AddProjectReference_Should_NotAddCopyToOutputDirectory()
    {
        var result = _editor.AddProjectReference(MinimalCsproj, "../Foo/Foo.csproj");
        Assert.DoesNotContain("CopyToOutputDirectory", result.Text);
    }
}
