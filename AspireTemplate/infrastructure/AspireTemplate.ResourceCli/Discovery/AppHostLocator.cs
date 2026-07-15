using System.Text.Json;
using System.Text.RegularExpressions;

namespace AspireTemplate.ResourceCli.Discovery;

/// <summary>
/// Locates an Aspire AppHost project using the resolution order defined in the plan:
/// 1. Explicit --root / --apphost path
/// 2. aspire.config.json in cwd or an ancestor
/// 3. Repository root solution → find AppHost projects in referenced .csproj files
/// 4. SDK markers (Aspire.AppHost.Sdk / IsAspireHost) via recursive .csproj search
/// 5. Interactive selection when multiple candidates exist
/// </summary>
public sealed class AppHostLocator
{
    private static readonly HashSet<string> ExcludedDirs =
        new(StringComparer.OrdinalIgnoreCase) { "bin", "obj", ".git", ".vs", "node_modules" };

    private static readonly Regex BuildAnchorPattern =
        new(@"builder\.Build\(\)\.(Run|RunAsync)\s*\(", RegexOptions.Compiled);

    private static readonly string[] ProgramFileNames = { "Program.cs", "AppHost.cs" };

    /// <param name="explicitRoot">Value of --root / --apphost if provided, otherwise null.</param>
    /// <param name="workingDirectory">Directory to use as cwd when no explicit root is given.</param>
    public DiscoveryResult Locate(string? explicitRoot = null, string? workingDirectory = null)
    {
        workingDirectory ??= Directory.GetCurrentDirectory();

        if (explicitRoot is not null)
            return LocateExplicit(explicitRoot);

        var configResult = TryLocateViaConfig(workingDirectory);
        if (configResult is not null)
            return configResult;

        var repoRoot = RepositoryLocator.FindRoot(workingDirectory);
        return LocateInDirectory(repoRoot);
    }

    // ── Explicit --root / --apphost ─────────────────────────────────────────

    private DiscoveryResult LocateExplicit(string rootPath)
    {
        if (rootPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            if (!File.Exists(rootPath))
                return new NoAppHostFound();
            return IsAppHostProject(rootPath)
                ? CreateResult(rootPath)
                : new NoAppHostFound();
        }

        if (Directory.Exists(rootPath))
            return LocateInDirectory(rootPath);

        return new NoAppHostFound();
    }

    // ── aspire.config.json ──────────────────────────────────────────────────

    private DiscoveryResult? TryLocateViaConfig(string startDir)
    {
        var dir = Path.GetFullPath(startDir);
        while (dir is not null)
        {
            var configPath = Path.Combine(dir, "aspire.config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
                    if (doc.RootElement.TryGetProperty("appHost", out var appHost) &&
                        appHost.TryGetProperty("path", out var pathProp))
                    {
                        var relative = pathProp.GetString();
                        if (!string.IsNullOrWhiteSpace(relative))
                        {
                            var absolute = Path.GetFullPath(Path.Combine(dir, relative));
                            if (File.Exists(absolute))
                                return CreateResult(absolute);
                        }
                    }
                }
                catch (JsonException) { /* malformed config — fall through */ }
                return null; // config present but path unresolvable
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    // ── Directory scan ──────────────────────────────────────────────────────

    private DiscoveryResult LocateInDirectory(string root)
    {
        var candidates = FindAppHostProjects(root);
        return candidates.Count switch
        {
            0 => new NoAppHostFound(),
            1 => CreateResult(candidates[0]),
            _ => new MultipleAppHostsFound(candidates)
        };
    }

    private static List<string> FindAppHostProjects(string root)
    {
        var result = new List<string>();
        CollectAppHostProjects(root, result);
        result.Sort(StringComparer.OrdinalIgnoreCase);
        return result;
    }

    private static void CollectAppHostProjects(string dir, List<string> result)
    {
        foreach (var csproj in Directory.GetFiles(dir, "*.csproj"))
        {
            if (IsAppHostProject(csproj))
                result.Add(csproj);
        }

        foreach (var sub in Directory.GetDirectories(dir))
        {
            if (ExcludedDirs.Contains(Path.GetFileName(sub)))
                continue;
            CollectAppHostProjects(sub, result);
        }
    }

    // ── Namespace detection ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the root namespace of the project at <paramref name="csprojPath"/>.
    /// Reads &lt;RootNamespace&gt; or &lt;AssemblyName&gt;; falls back to the project file name.
    /// </summary>
    internal static string DetectRootNamespace(string csprojPath)
    {
        try
        {
            var content = File.ReadAllText(csprojPath);
            var ns = ExtractXmlValue(content, "RootNamespace")
                  ?? ExtractXmlValue(content, "AssemblyName");
            if (!string.IsNullOrWhiteSpace(ns)) return ns;
        }
        catch (IOException) { }
        return Path.GetFileNameWithoutExtension(csprojPath);
    }

    private static string? ExtractXmlValue(string xml, string elementName)
    {
        var match = Regex.Match(xml, $@"<{elementName}>(.*?)</{elementName}>", RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    // ── AppHost marker detection ────────────────────────────────────────────

    internal static bool IsAppHostProject(string csprojPath)
    {
        try
        {
            var content = File.ReadAllText(csprojPath);
            return content.Contains("Aspire.AppHost.Sdk", StringComparison.Ordinal) ||
                   content.Contains("<IsAspireHost>true</IsAspireHost>", StringComparison.OrdinalIgnoreCase);
        }
        catch (IOException)
        {
            return false;
        }
    }

    // ── Build result construction ───────────────────────────────────────────

    private DiscoveryResult CreateResult(string csprojPath)
    {
        var dir = Path.GetDirectoryName(csprojPath)!;
        var programFile = FindProgramFile(dir);
        if (programFile is null)
            return new NoAppHostFound();

        var anchorLine = FindBuildAnchorLine(programFile);
        if (anchorLine < 0)
            return new BuildAnchorMissing(csprojPath, programFile);

        return new AppHostFound(csprojPath, programFile, anchorLine);
    }

    private static string? FindProgramFile(string dir)
    {
        foreach (var name in ProgramFileNames)
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path)) return path;
        }
        return null;
    }

    /// <returns>1-based line number of the build anchor, or -1 if not found.</returns>
    internal static int FindBuildAnchorLine(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        for (var i = 0; i < lines.Length; i++)
        {
            if (BuildAnchorPattern.IsMatch(lines[i]))
                return i + 1;
        }
        return -1;
    }
}
