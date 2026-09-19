using System.IO.Compression;
using System.Text;

namespace OfflineAvMirror.Core.Tests;

public sealed class WindowsMirrorTests : IDisposable
{
    private const string Base = "http://geo.mirror.test/xmlzone/line1/release/1220/windows";

    private readonly TestFiles _files = new();
    private readonly FakeMirror _mirror = new();

    public void Dispose()
    {
        _mirror.Dispose();
        _files.Dispose();
    }

    /// <summary>
    /// A two-level tree like the real one: entry point, revision list, one
    /// component index that declares a compressed base.
    /// </summary>
    private void ServeTree(string componentPath = "90/av-engine/9/revision.xml")
    {
        var lzma = Encoding.ASCII.GetBytes("compressed base");
        var component = Encoding.UTF8.GetBytes($"""
            <revision>
              <file name="common/drw.vdb" hash="ignored" size="99">
                <lzma name="common/drw.vdb.lzma" hash="{TestFiles.Sha256(lzma)}" size="{lzma.Length}"/>
              </file>
            </revision>
            """);
        var revision = $"""<revision><xml name="{componentPath}" hash="{TestFiles.Sha256(component)}"/></revision>""";

        _mirror.Add($"{Base}/vdb-versions.xml",
            """<versions><version name="1.0"><timestamp value="20260101120000"/></version></versions>""");
        _mirror.Add($"{Base}/1.0/vdb-revision.xml", revision);
        _mirror.Add($"{Base}/{componentPath}", component);
        var dir = componentPath[..componentPath.LastIndexOf('/')];
        _mirror.Add($"{Base}/{dir}/common/drw.vdb.lzma", lzma);
    }

    private MirrorJob Job(bool archive) => new(new MirrorOptions
    {
        KeyPath = _files.Write("drweb32.key", TestFiles.Key()),
        ZonePath = _files.Write("drwzones.xml", TestFiles.WindowsZone),
        OutputDirectory = _files.Dir("out"),
        CreateArchive = archive,
        RequiredFreeMegabytes = 0,
    }, _mirror);

    [Fact]
    public async Task Mirror_folder_is_named_and_laid_out_like_the_vendor_one()
    {
        ServeTree();

        var result = await Job(archive: false).RunAsync(null, TestContext.Current.CancellationToken);

        var root = Path.Combine(result.OutputPath, WindowsPlanner.RootDirectory);
        Assert.True(File.Exists(Path.Combine(root, "vdb-versions.xml")));
        Assert.True(File.Exists(Path.Combine(root, "1.0", "vdb-revision.xml")));
        Assert.True(File.Exists(Path.Combine(root, "90", "av-engine", "9", "common", "drw.vdb.lzma")));
        Assert.False(File.Exists(Path.Combine(root, "90", "av-engine", "9", "common", "drw.vdb")));
        Assert.Equal(1, result.Files);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), result.BasesTimestamp);
    }

    [Fact]
    public async Task Archive_is_an_uncompressed_zip()
    {
        ServeTree();

        var result = await Job(archive: true).RunAsync(null, TestContext.Current.CancellationToken);

        Assert.EndsWith(".zip", result.OutputPath, StringComparison.Ordinal);
        using var zip = ZipFile.OpenRead(result.OutputPath);
        var set = Path.GetFileNameWithoutExtension(result.OutputPath);
        var lzma = zip.GetEntry($"{set}/{WindowsPlanner.RootDirectory}/90/av-engine/9/common/drw.vdb.lzma");
        Assert.NotNull(lzma);
        Assert.Equal(lzma.Length, lzma.CompressedLength);
    }

    [Fact]
    public async Task A_component_index_that_escapes_the_mirror_is_refused()
    {
        ServeTree(componentPath: "../outside/revision.xml");

        await Assert.ThrowsAsync<UserFacingException>(() => Job(archive: false).RunAsync(null, TestContext.Current.CancellationToken));
    }
}
