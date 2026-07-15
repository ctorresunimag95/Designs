using AspireTemplate.ResourceCli.Discovery;
using Xunit;

namespace AspireTemplate.ResourceCli.Tests.Discovery;

public class AppHostLocatorTests
{
    private static string FixtureDir(string relative) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "AppHosts", relative);

    private readonly AppHostLocator _locator = new();

    // ── SDK marker ──────────────────────────────────────────────────────────

    [Fact]
    public void Locate_Should_FindAppHostViaSdkMarker()
    {
        var result = _locator.Locate(explicitRoot: FixtureDir("SdkMarker"));
        Assert.IsType<AppHostFound>(result);
    }

    [Fact]
    public void Locate_Should_ReturnValidProjectPathForSdkMarker()
    {
        var result = (AppHostFound)_locator.Locate(explicitRoot: FixtureDir("SdkMarker"));
        Assert.EndsWith(".csproj", result.ProjectPath, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(result.ProjectPath));
    }

    [Fact]
    public void Locate_Should_DetectBuildAnchorForSdkMarker()
    {
        var result = (AppHostFound)_locator.Locate(explicitRoot: FixtureDir("SdkMarker"));
        Assert.True(result.BuildAnchorLine > 0);
    }

    // ── IsAspireHost marker ─────────────────────────────────────────────────

    [Fact]
    public void Locate_Should_FindAppHostViaIsAspireHostMarker()
    {
        var result = _locator.Locate(explicitRoot: FixtureDir("IsAspireHostMarker"));
        Assert.IsType<AppHostFound>(result);
    }

    [Fact]
    public void Locate_Should_DetectBuildAnchorForIsAspireHostMarker()
    {
        var result = (AppHostFound)_locator.Locate(explicitRoot: FixtureDir("IsAspireHostMarker"));
        Assert.True(result.BuildAnchorLine > 0);
    }

    // ── aspire.config.json ──────────────────────────────────────────────────

    [Fact]
    public void Locate_Should_FindAppHostViaAspireConfigJson()
    {
        var dir = FixtureDir("ConfigBased");
        var result = _locator.Locate(workingDirectory: dir);
        Assert.IsType<AppHostFound>(result);
    }

    [Fact]
    public void Locate_Should_ResolveProjectPathFromAspireConfigJson()
    {
        var dir = FixtureDir("ConfigBased");
        var result = (AppHostFound)_locator.Locate(workingDirectory: dir);
        Assert.Contains("MyAppHost", result.ProjectPath, StringComparison.OrdinalIgnoreCase);
    }

    // ── Multiple candidates ─────────────────────────────────────────────────

    [Fact]
    public void Locate_Should_ReturnMultipleFoundWhenSeveralAppHostsExist()
    {
        var result = _locator.Locate(explicitRoot: FixtureDir("Multiple"));
        Assert.IsType<MultipleAppHostsFound>(result);
    }

    [Fact]
    public void Locate_Should_ListAllCandidatesWhenMultipleAppHostsExist()
    {
        var result = (MultipleAppHostsFound)_locator.Locate(explicitRoot: FixtureDir("Multiple"));
        Assert.Equal(2, result.Candidates.Count);
    }

    // ── None found ──────────────────────────────────────────────────────────

    [Fact]
    public void Locate_Should_ReturnNoAppHostFoundWhenNonePresent()
    {
        var result = _locator.Locate(explicitRoot: FixtureDir("None"));
        Assert.IsType<NoAppHostFound>(result);
    }

    // ── Missing build anchor ────────────────────────────────────────────────

    [Fact]
    public void Locate_Should_ReturnBuildAnchorMissingWhenRunCallAbsent()
    {
        var result = _locator.Locate(explicitRoot: FixtureDir("NoBuildAnchor"));
        Assert.IsType<BuildAnchorMissing>(result);
    }

    // ── Explicit .csproj path ───────────────────────────────────────────────

    [Fact]
    public void Locate_Should_FindAppHostGivenExplicitCsprojPath()
    {
        var csproj = Path.Combine(FixtureDir("SdkMarker"), "SdkMarkerAppHost.csproj");
        var result = _locator.Locate(explicitRoot: csproj);
        Assert.IsType<AppHostFound>(result);
    }

    [Fact]
    public void Locate_Should_ReturnNoAppHostFoundForNonAppHostCsproj()
    {
        var csproj = Path.Combine(FixtureDir("None"), "SomeProject.csproj");
        var result = _locator.Locate(explicitRoot: csproj);
        Assert.IsType<NoAppHostFound>(result);
    }

    // ── IsAppHostProject ────────────────────────────────────────────────────

    [Fact]
    public void IsAppHostProject_Should_ReturnTrueForSdkMarkerCsproj()
    {
        var csproj = Path.Combine(FixtureDir("SdkMarker"), "SdkMarkerAppHost.csproj");
        Assert.True(AppHostLocator.IsAppHostProject(csproj));
    }

    [Fact]
    public void IsAppHostProject_Should_ReturnTrueForIsAspireHostCsproj()
    {
        var csproj = Path.Combine(FixtureDir("IsAspireHostMarker"), "IsAspireHostMarker.csproj");
        Assert.True(AppHostLocator.IsAppHostProject(csproj));
    }

    [Fact]
    public void IsAppHostProject_Should_ReturnFalseForPlainCsproj()
    {
        var csproj = Path.Combine(FixtureDir("None"), "SomeProject.csproj");
        Assert.False(AppHostLocator.IsAppHostProject(csproj));
    }

    // ── FindBuildAnchorLine ─────────────────────────────────────────────────

    [Fact]
    public void FindBuildAnchorLine_Should_DetectRunAsyncCall()
    {
        var program = Path.Combine(FixtureDir("IsAspireHostMarker"), "Program.cs");
        Assert.True(AppHostLocator.FindBuildAnchorLine(program) > 0);
    }

    [Fact]
    public void FindBuildAnchorLine_Should_DetectRunCall()
    {
        var program = Path.Combine(FixtureDir("SdkMarker"), "Program.cs");
        Assert.True(AppHostLocator.FindBuildAnchorLine(program) > 0);
    }

    [Fact]
    public void FindBuildAnchorLine_Should_ReturnMinusOneWhenAnchorAbsent()
    {
        var program = Path.Combine(FixtureDir("NoBuildAnchor"), "Program.cs");
        Assert.Equal(-1, AppHostLocator.FindBuildAnchorLine(program));
    }

    // ── RepositoryLocator ───────────────────────────────────────────────────

    [Fact]
    public void RepositoryLocator_Should_WalkUpToGitOrSlnRoot()
    {
        var start = FixtureDir("SdkMarker");
        var root = RepositoryLocator.FindRoot(start);
        var hasGit = Directory.Exists(Path.Combine(root, ".git"));
        var hasSln = Directory.GetFiles(root, "*.sln").Length > 0 ||
                     Directory.GetFiles(root, "*.slnx").Length > 0;
        Assert.True(hasGit || hasSln, $"Root '{root}' has no .git or solution file");
    }

    // ── SolutionLocator ─────────────────────────────────────────────────────

    [Fact]
    public void SolutionLocator_Should_FindSolutionFileFromRepoRoot()
    {
        var repoRoot = RepositoryLocator.FindRoot(AppContext.BaseDirectory);
        var solutions = SolutionLocator.Find(repoRoot);
        Assert.NotEmpty(solutions);
    }
}
