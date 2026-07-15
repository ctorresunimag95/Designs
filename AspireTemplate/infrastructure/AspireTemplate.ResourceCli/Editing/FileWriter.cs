using System.Text;
using AspireTemplate.ResourceCli.Templates;

namespace AspireTemplate.ResourceCli.Editing;

public sealed class FileWriter : IDisposable
{
    private readonly List<(string Path, byte[]? Backup)> _backups = [];
    private bool _committed;

    public void WriteText(string path, string content, Encoding encoding)
    {
        EnsureOpen();
        BackupIfExists(path);
        EnsureDirectory(path);
        AtomicWriteBytes(path, encoding.GetBytes(content));
    }

    public void CopyCompanion(FileCopyOperation op)
    {
        EnsureOpen();
        EnsureDirectory(op.DestinationPath);

        byte[] bytes;
        if (op.Namespace is not null)
        {
            var text = TemplateLoader.ReadCompanionFile(op.SourcePath)
                .Replace("{{Namespace}}", op.Namespace, StringComparison.Ordinal);
            bytes = Encoding.UTF8.GetBytes(text);
        }
        else
        {
            using var srcStream = TemplateLoader.OpenCompanionFile(op.SourcePath);
            using var ms = new MemoryStream();
            srcStream.CopyTo(ms);
            bytes = ms.ToArray();
        }

        if (File.Exists(op.DestinationPath))
        {
            var existing = File.ReadAllBytes(op.DestinationPath);
            if (existing.SequenceEqual(bytes)) return; // already identical — idempotent
            if (!op.Required) return;                  // optional conflict warned upstream — skip
            throw new InvalidOperationException(
                $"Companion file '{op.DestinationPath}' already exists with different content.");
        }

        BackupIfExists(op.DestinationPath);
        AtomicWriteBytes(op.DestinationPath, bytes);
    }

    public void Commit() => _committed = true;

    public void Rollback()
    {
        foreach (var (path, backup) in Enumerable.Reverse(_backups))
        {
            try
            {
                if (backup is null)
                {
                    if (File.Exists(path)) File.Delete(path);
                }
                else
                {
                    File.WriteAllBytes(path, backup);
                }
            }
            catch { /* best-effort */ }
        }
        _backups.Clear();
    }

    public void Dispose()
    {
        if (!_committed) Rollback();
    }

    private void EnsureOpen()
    {
        if (_committed) throw new InvalidOperationException("FileWriter has already been committed.");
    }

    private void BackupIfExists(string path) =>
        _backups.Add(File.Exists(path) ? (path, File.ReadAllBytes(path)) : (path, null));

    private static void EnsureDirectory(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    private static void AtomicWriteBytes(string path, byte[] bytes)
    {
        var tmp = path + ".AspireTemplate-tmp";
        try
        {
            File.WriteAllBytes(tmp, bytes);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tmp)) File.Delete(tmp);
            throw;
        }
    }
}
