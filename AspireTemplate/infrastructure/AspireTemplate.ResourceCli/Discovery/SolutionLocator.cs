namespace AspireTemplate.ResourceCli.Discovery;

/// <summary>Finds .sln and .slnx files under a directory tree.</summary>
public static class SolutionLocator
{
    public static IReadOnlyList<string> Find(string rootPath, bool recursive = true)
    {
        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var results = new List<string>();
        results.AddRange(Directory.GetFiles(rootPath, "*.sln", option));
        results.AddRange(Directory.GetFiles(rootPath, "*.slnx", option));
        results.Sort(StringComparer.OrdinalIgnoreCase);
        return results;
    }
}
