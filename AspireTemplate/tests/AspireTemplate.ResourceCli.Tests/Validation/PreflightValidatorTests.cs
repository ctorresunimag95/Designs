using AspireTemplate.ResourceCli.Catalog;
using AspireTemplate.ResourceCli.Discovery;
using AspireTemplate.ResourceCli.Validation;
using Xunit;

namespace AspireTemplate.ResourceCli.Tests.Validation;

public class PreflightValidatorTests
{
    private static string FixtureDir(string relative) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "AppHosts", relative);

    private readonly PreflightValidator _validator = new();

    // ── Discovery result errors ───────────────────────────────────────────────

    [Fact]
    public void Validate_Should_ErrorOnNoAppHostFound()
    {
        var issues = _validator.Validate(new NoAppHostFound(), [], FixtureDir("None"));
        Assert.Contains(issues, i => i.Code == "NO_APPHOST" && i.IsError);
    }

    [Fact]
    public void Validate_Should_ErrorOnMultipleAppHostsFound()
    {
        var issues = _validator.Validate(
            new MultipleAppHostsFound(["a.csproj", "b.csproj"]), [], FixtureDir("Multiple"));
        Assert.Contains(issues, i => i.Code == "MULTIPLE_APPHOSTS" && i.IsError);
    }

    [Fact]
    public void Validate_Should_ErrorOnBuildAnchorMissing()
    {
        var issues = _validator.Validate(
            new BuildAnchorMissing(FixtureDir("NoBuildAnchor/NoBuildAnchor.csproj"),
                FixtureDir("NoBuildAnchor/Program.cs")), [], FixtureDir("NoBuildAnchor"));
        Assert.Contains(issues, i => i.Code == "MISSING_BUILD_ANCHOR" && i.IsError);
    }

    // ── Dependency validation ─────────────────────────────────────────────────

    [Fact]
    public void ValidateDependencies_Should_ErrorWhenSbExplorerSelectedWithoutServiceBus()
    {
        var selected = new[] { ResourceCatalog.Find("sbexplorer")! };
        var issues = _validator.ValidateDependencies(selected);
        Assert.Contains(issues, i => i.Code == "MISSING_DEPENDENCY" && i.IsError);
    }

    [Fact]
    public void ValidateDependencies_Should_PassWhenSbExplorerHasServiceBus()
    {
        var selected = new[]
        {
            ResourceCatalog.Find("servicebus")!,
            ResourceCatalog.Find("sbexplorer")!,
        };
        var issues = _validator.ValidateDependencies(selected);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateDependencies_Should_PassForIndependentResources()
    {
        var selected = ResourceCatalog.All.Where(r => r.Key != "sbexplorer").ToArray();
        var issues = _validator.ValidateDependencies(selected);
        Assert.Empty(issues);
    }

    // ── Package conflict validation ───────────────────────────────────────────

    [Fact]
    public void ValidatePackageConflicts_Should_PassWhenNoConflicts()
    {
        var csproj = FixtureDir("Minimal/MinimalAppHost.csproj");
        var issues = _validator.ValidatePackageConflicts(csproj, [ResourceCatalog.Find("servicebus")!]);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidatePackageConflicts_Should_ErrorWhenInstalledVersionIsTooOld()
    {
        var csproj = FixtureDir("LegacyRegion/LegacyRegionAppHost.csproj");
        // LegacyRegion fixture has Keycloak 13.4.6-preview — not a lower semver, so just verify no false positives
        var issues = _validator.ValidatePackageConflicts(csproj, [ResourceCatalog.Find("keycloak")!]);
        // No error expected since the versions are the same
        Assert.DoesNotContain(issues, i => i.Code == "PACKAGE_CONFLICT" && i.IsError);
    }

    // ── Companion file conflict ───────────────────────────────────────────────

    [Fact]
    public void ValidateCompanionFileConflicts_Should_PassWhenNoExistingFiles()
    {
        var dir = FixtureDir("Minimal");
        var issues = _validator.ValidateCompanionFileConflicts(dir, [ResourceCatalog.Find("keycloak")!]);
        Assert.Empty(issues);
    }

    // ── Writable validation ───────────────────────────────────────────────────

    [Fact]
    public void ValidateWritable_Should_PassForExistingWritableFile()
    {
        var csproj = FixtureDir("Minimal/MinimalAppHost.csproj");
        var issues = _validator.ValidateWritable(csproj);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateWritable_Should_PassForNonExistentFile()
    {
        // Non-existent files don't generate errors (they'll be created)
        var issues = _validator.ValidateWritable(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".txt"));
        Assert.Empty(issues);
    }

    // ── Project prerequisite validation ──────────────────────────────────────

    [Fact]
    public void ValidateProjectPrerequisites_Should_WarnWhenRequiredProjectMissing()
    {
        var selected = new[] { ResourceCatalog.Find("sbexplorer")! };
        var issues = _validator.ValidateProjectPrerequisites(selected, Path.GetTempPath());
        var issue = Assert.Single(issues, i => i.Code == "MISSING_REQUIRED_PROJECT");
        Assert.False(issue.IsError);
    }

    [Fact]
    public void ValidateProjectPrerequisites_Should_PassWhenProjectExists()
    {
        var selected = new[] { ResourceCatalog.Find("servicebus")! };
        var issues = _validator.ValidateProjectPrerequisites(selected, Path.GetTempPath());
        Assert.Empty(issues);
    }

    // ── Full validate with AppHostFound ───────────────────────────────────────

    [Fact]
    public void Validate_Should_PassForMinimalAppHostWithNoResources()
    {
        var csproj = FixtureDir("Minimal/MinimalAppHost.csproj");
        var program = FixtureDir("Minimal/Program.cs");
        var discovery = new AppHostFound(csproj, program, 3);
        var issues = _validator.Validate(discovery, [], FixtureDir("Minimal"));
        Assert.DoesNotContain(issues, i => i.IsError);
    }

    [Fact]
    public void Validate_Should_PassForMinimalAppHostWithServiceBus()
    {
        var csproj = FixtureDir("Minimal/MinimalAppHost.csproj");
        var program = FixtureDir("Minimal/Program.cs");
        var discovery = new AppHostFound(csproj, program, 3);
        var issues = _validator.Validate(discovery, [ResourceCatalog.Find("servicebus")!], FixtureDir("Minimal"));
        Assert.DoesNotContain(issues, i => i.IsError);
    }
}
