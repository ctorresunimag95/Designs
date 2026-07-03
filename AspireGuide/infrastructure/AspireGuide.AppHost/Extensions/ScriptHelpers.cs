namespace AspireGuide.AppHost.Extensions;

public static class ScriptHelpers
{
    public static string LoadAllScriptsFromScriptsFolder()
        => LoadAllScriptsFromSubfolder("Init");

    public static string LoadAllScriptsFromPostInitFolder()
        => LoadAllScriptsFromSubfolder("PostInit");

    private static string LoadAllScriptsFromSubfolder(string subfolder)
    {
        var scriptsDir = ResolveScriptsDirectory(subfolder);
        if (scriptsDir is null)
            return string.Empty;

        var files = Directory.GetFiles(scriptsDir, "*.sql")
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return string.Join(Environment.NewLine, files.Select(File.ReadAllText));
    }

    private static string? ResolveScriptsDirectory(string subfolder)
    {
        var candidate = Path.Combine(AppContext.BaseDirectory, "Scripts", subfolder);
        if (Directory.Exists(candidate))
            return candidate;

        var fallback = Path.Combine(Directory.GetCurrentDirectory(), "Scripts", subfolder);
        return Directory.Exists(fallback) ? fallback : null;
    }
}
