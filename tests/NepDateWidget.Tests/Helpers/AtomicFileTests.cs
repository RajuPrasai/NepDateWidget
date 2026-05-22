using System.IO;
using System.Reflection;

namespace NepDateWidget.Tests.Helpers;

/// <summary>
/// Tests for the internal <c>AtomicFile.WriteAllText</c> helper.
/// Accessed via reflection because the type is internal to the main assembly
/// and the test project does not have <c>InternalsVisibleTo</c>.
/// </summary>
public class AtomicFileTests : IDisposable
{
    private readonly string _dir;

    public AtomicFileTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "NepDateWidget.AtomicFile." + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private static bool Write(string path, string contents)
    {
        var asm = typeof(NepDateWidget.App).Assembly;
        var t = asm.GetType("NepDateWidget.Helpers.AtomicFile", throwOnError: true)!;
        var m = t.GetMethod("WriteAllText", BindingFlags.Public | BindingFlags.Static)!;
        return (bool)m.Invoke(null, new object[] { path, contents })!;
    }

    [Fact]
    public void WriteAllText_FreshFile_CreatesFileWithContent()
    {
        var path = Path.Combine(_dir, "settings.json");
        var ok = Write(path, "{\"a\":1}");
        Assert.True(ok);
        Assert.True(File.Exists(path));
        Assert.Equal("{\"a\":1}", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_ExistingFile_OverwritesContent()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "OLD");
        var ok = Write(path, "NEW");
        Assert.True(ok);
        Assert.Equal("NEW", File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_RemovesBackupFile()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "OLD");
        Write(path, "NEW");
        Assert.False(File.Exists(path + ".bak"));
    }

    [Fact]
    public void WriteAllText_RemovesTempFile_OnSuccess()
    {
        var path = Path.Combine(_dir, "settings.json");
        Write(path, "v1");
        Write(path, "v2");
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public void WriteAllText_EmptyContent_WritesEmptyFile()
    {
        var path = Path.Combine(_dir, "empty.json");
        var ok = Write(path, string.Empty);
        Assert.True(ok);
        Assert.Equal(string.Empty, File.ReadAllText(path));
    }

    [Fact]
    public void WriteAllText_InvalidPath_ReturnsFalseInsteadOfThrowing()
    {
        // A path under a missing directory throws DirectoryNotFoundException
        // (an IOException). The helper must swallow it and return false.
        var path = Path.Combine(_dir, "does", "not", "exist", "x.json");
        var ok = Write(path, "x");
        Assert.False(ok);
    }

    [Fact]
    public void WriteAllText_RepeatedWrites_PreserveLatestContent()
    {
        var path = Path.Combine(_dir, "settings.json");
        for (int i = 0; i < 10; i++)
        {
            Write(path, "v" + i);
        }

        Assert.Equal("v9", File.ReadAllText(path));
        Assert.False(File.Exists(path + ".bak"));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
