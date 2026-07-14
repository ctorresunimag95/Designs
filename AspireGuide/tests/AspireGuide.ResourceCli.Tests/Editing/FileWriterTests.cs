using AspireGuide.ResourceCli.Catalog;
using AspireGuide.ResourceCli.Editing;
using System.Text;
using Xunit;

namespace AspireGuide.ResourceCli.Tests.Editing;

public class FileWriterTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "fw-tests-" + Guid.NewGuid().ToString("N")[..8]);

    public FileWriterTests() => Directory.CreateDirectory(_tempDir);

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    private string TempFile(string name, string? content = null)
    {
        var path = Path.Combine(_tempDir, name);
        if (content is not null) File.WriteAllText(path, content);
        return path;
    }

    // ── WriteText ─────────────────────────────────────────────────────────────

    [Fact]
    public void WriteText_Should_CreateFileWithCorrectContent()
    {
        var path = TempFile("out.cs");
        using var writer = new FileWriter();
        writer.WriteText(path, "hello world", Encoding.UTF8);
        writer.Commit();
        Assert.Equal("hello world", File.ReadAllText(path));
    }

    [Fact]
    public void WriteText_Should_OverwriteExistingFile()
    {
        var path = TempFile("out.cs", "original");
        using var writer = new FileWriter();
        writer.WriteText(path, "updated", Encoding.UTF8);
        writer.Commit();
        Assert.Equal("updated", File.ReadAllText(path));
    }

    [Fact]
    public void WriteText_Should_CreateParentDirectoriesIfMissing()
    {
        var path = Path.Combine(_tempDir, "sub", "nested", "file.cs");
        using var writer = new FileWriter();
        writer.WriteText(path, "content", Encoding.UTF8);
        writer.Commit();
        Assert.True(File.Exists(path));
    }

    // ── Rollback ──────────────────────────────────────────────────────────────

    [Fact]
    public void Rollback_Should_RestoreOriginalContent()
    {
        var path = TempFile("out.cs", "original");
        using var writer = new FileWriter();
        writer.WriteText(path, "modified", Encoding.UTF8);
        writer.Rollback();
        Assert.Equal("original", File.ReadAllText(path));
    }

    [Fact]
    public void Rollback_Should_DeleteNewlyCreatedFile()
    {
        var path = TempFile("new.cs");
        using var writer = new FileWriter();
        writer.WriteText(path, "new content", Encoding.UTF8);
        writer.Rollback();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void Dispose_Without_Commit_Should_Rollback()
    {
        var path = TempFile("out.cs", "before");
        using (var writer = new FileWriter())
        {
            writer.WriteText(path, "after", Encoding.UTF8);
            // no commit — dispose should rollback
        }
        Assert.Equal("before", File.ReadAllText(path));
    }

    [Fact]
    public void Dispose_After_Commit_Should_Not_Rollback()
    {
        var path = TempFile("out.cs", "before");
        using (var writer = new FileWriter())
        {
            writer.WriteText(path, "after", Encoding.UTF8);
            writer.Commit();
        }
        Assert.Equal("after", File.ReadAllText(path));
    }

    [Fact]
    public void Rollback_Should_RestoreMultipleFiles()
    {
        var a = TempFile("a.cs", "a-original");
        var b = TempFile("b.cs", "b-original");
        using var writer = new FileWriter();
        writer.WriteText(a, "a-modified", Encoding.UTF8);
        writer.WriteText(b, "b-modified", Encoding.UTF8);
        writer.Rollback();
        Assert.Equal("a-original", File.ReadAllText(a));
        Assert.Equal("b-original", File.ReadAllText(b));
    }

    // ── CopyCompanion ─────────────────────────────────────────────────────────

    [Fact]
    public void CopyCompanion_Should_CopyEmbeddedFileToDestination()
    {
        var dest = Path.Combine(_tempDir, "Extensions", "ServiceBusExtensions.cs");
        var op = new FileCopyOperation("Extensions/ServiceBusExtensions.cs", dest, Required: true);
        using var writer = new FileWriter();
        writer.CopyCompanion(op);
        writer.Commit();
        Assert.True(File.Exists(dest));
        Assert.True(new FileInfo(dest).Length > 0);
    }

    [Fact]
    public void CopyCompanion_Should_BeIdempotentWhenContentMatches()
    {
        var dest = Path.Combine(_tempDir, "Extensions", "ServiceBusExtensions.cs");
        var op = new FileCopyOperation("Extensions/ServiceBusExtensions.cs", dest, Required: true);

        // First copy
        using (var w1 = new FileWriter()) { w1.CopyCompanion(op); w1.Commit(); }
        var firstWrite = File.GetLastWriteTimeUtc(dest);

        // Second copy — file already exists with identical content — should skip
        using (var w2 = new FileWriter()) { w2.CopyCompanion(op); w2.Commit(); }
        Assert.Equal(firstWrite, File.GetLastWriteTimeUtc(dest));
    }

    [Fact]
    public void CopyCompanion_Optional_Should_SkipWhenFileExistsWithDifferentContent()
    {
        var dest = TempFile("Sample.txt", "user-data");
        var op = new FileCopyOperation("Content/SampleFiles/Sample.txt", dest, Required: false);
        using var writer = new FileWriter();
        writer.CopyCompanion(op); // should NOT throw
        writer.Commit();
        Assert.Equal("user-data", File.ReadAllText(dest)); // unchanged
    }

    [Fact]
    public void CopyCompanion_Required_Should_ThrowWhenFileExistsWithDifferentContent()
    {
        var dest = TempFile("realm-export.json", "{}");
        var op = new FileCopyOperation("Keycloak/realm-export.json", dest, Required: true);
        using var writer = new FileWriter();
        Assert.Throws<InvalidOperationException>(() => writer.CopyCompanion(op));
    }

    [Fact]
    public void CopyCompanion_Rollback_Should_DeleteNewlyCreatedFile()
    {
        var dest = Path.Combine(_tempDir, "ext", "BlobExtensions.cs");
        var op = new FileCopyOperation("Extensions/BlobExtensions.cs", dest, Required: true);
        using var writer = new FileWriter();
        writer.CopyCompanion(op);
        writer.Rollback();
        Assert.False(File.Exists(dest));
    }
}
