namespace AspireGuide.ResourceCli.Discovery;

/// <summary>
/// Walks the directory tree upward to locate the repository / workspace root.
/// A root is identified by a .git directory or a solution file (.sln / .slnx).
/// Falls back to the start path if no marker is found.
/// </summary>
public static class RepositoryLocator
{
    public static string FindRoot(string startPath)
    {
        var dir = Path.GetFullPath(startPath);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;

            if (Directory.GetFiles(dir, "*.sln").Length > 0 ||
                Directory.GetFiles(dir, "*.slnx").Length > 0)
                return dir;

            dir = Path.GetDirectoryName(dir);
        }

        return Path.GetFullPath(startPath);
    }
}
