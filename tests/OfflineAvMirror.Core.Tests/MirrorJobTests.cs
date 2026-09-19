using System.Formats.Tar;
using System.IO.Compression;
using System.Text;

namespace OfflineAvMirror.Core.Tests;

public sealed class MirrorJobTests : IDisposable
{
    private const string Primary = "http://primary.mirror.test/unix/line1/1110/vdb64";
    private const string Fallback = "http://fallback1.mirror.test/unix/line1/1110/vdb64";

    private static readonly byte[] BaseA = Encoding.ASCII.GetBytes("virus base A");
    private static readonly byte[] BaseB = Encoding.ASCII.GetBytes("virus base B, longer");

    private readonly TestFiles _files = new();
    private readonly FakeMirror _mirror = new();
    private readonly string _out;

    public MirrorJobTests() => _out = _files.Dir("out");

    public void Dispose()
    {
        _mirror.Dispose();
        _files.Dispose();
    }

    private static string Index(string shaA) =>
        $"[DrWebUpdateList]\n[1700000000]\n+a.vdb, {shaA}, {BaseA.Length}\n+b.vdb, {TestFiles.Sha256(BaseB)}, {BaseB.Length}\n[DrWebUpdateListEnd]\n";

    private void Serve(string baseUrl, string? shaA = null)
    {
        _mirror.Add($"{baseUrl}/version2.lst", Index(shaA ?? TestFiles.Sha256(BaseA)));
        _mirror.Add($"{baseUrl}/a.vdb", BaseA);
        _mirror.Add($"{baseUrl}/b.vdb", BaseB);
    }

    private MirrorJob Job(bool archive = false, string? zone = null) => new(new MirrorOptions
    {
        KeyPath = _files.Write("drweb32.key", TestFiles.Key()),
        ZonePath = _files.Write("zone.ini", zone ?? TestFiles.UnixZone),
        OutputDirectory = _out,
        CreateArchive = archive,
        RequiredFreeMegabytes = 0,
    }, _mirror);

    private string[] OutputEntries() =>
        Directory.GetFileSystemEntries(_out).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray()!;

    [Fact]
    public async Task Unix_mirror_has_the_vendor_layout()
    {
        Serve(Primary);

        var result = await Job().RunAsync(null, TestContext.Current.CancellationToken);

        Assert.False(result.IsArchive);
        Assert.Equal(2, result.Files);
        var line = Path.Combine(result.OutputPath, "vdb64");
        Assert.Equal(BaseA, File.ReadAllBytes(Path.Combine(line, "a.vdb")));
        Assert.True(File.Exists(Path.Combine(line, "version2.lst")));
        Assert.Equal([Path.GetFileName(result.OutputPath)], OutputEntries());
    }

    [Fact]
    public async Task The_key_hash_is_what_admits_the_client()
    {
        Serve(Primary);
        var job = Job();
        job.Preflight();
        _mirror.AdmittedKeyMd5 = job.Key!.Md5;

        await job.RunAsync(null, TestContext.Current.CancellationToken);

        Assert.All(_mirror.Requests, r => Assert.Equal(job.Key.Md5, r.Headers.GetValues("X-DrWeb-Validate").Single()));
    }

    [Fact]
    public async Task Archive_is_a_tar_gz_with_a_sha256sum_file()
    {
        Serve(Primary);

        var result = await Job(archive: true).RunAsync(null, TestContext.Current.CancellationToken);

        Assert.True(result.IsArchive);
        var name = Path.GetFileName(result.OutputPath);
        Assert.EndsWith(".tar.gz", name, StringComparison.Ordinal);
        Assert.Equal([name, name + ".sha256"], OutputEntries());

        var sum = File.ReadAllText(result.OutputPath + ".sha256");
        Assert.Equal($"{TestFiles.Sha256(File.ReadAllBytes(result.OutputPath))}  {name}\n", sum);

        await using var gz = new GZipStream(File.OpenRead(result.OutputPath), CompressionMode.Decompress);
        await using var tar = new TarReader(gz);
        var set = name[..^".tar.gz".Length];
        var entries = new List<string>();
        while (await tar.GetNextEntryAsync(cancellationToken: TestContext.Current.CancellationToken) is { } e)
            entries.Add(e.Name.TrimEnd('/'));
        Assert.Contains($"{set}/vdb64/a.vdb", entries);
    }

    [Fact]
    public async Task A_dead_primary_falls_back_to_the_next_mirror()
    {
        _mirror.TakeDown("primary.mirror.test");
        Serve(Fallback);

        var result = await Job().RunAsync(null, TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Files);
        Assert.Contains(_mirror.Requests, r => r.RequestUri!.Host == "fallback1.mirror.test");
    }

    [Fact]
    public async Task A_license_refusal_is_not_retried_on_other_mirrors()
    {
        Serve(Primary);
        Serve(Fallback);
        _mirror.AdmittedKeyMd5 = "not-this-key";

        await Assert.ThrowsAsync<MirrorRefusedException>(() => Job().RunAsync(null, TestContext.Current.CancellationToken));

        Assert.Single(_mirror.Requests);
        Assert.Empty(OutputEntries());
    }

    [Fact]
    public async Task A_corrupt_file_leaves_nothing_behind()
    {
        Serve(Primary, shaA: new string('0', 64));

        await Assert.ThrowsAsync<UserFacingException>(() => Job().RunAsync(null, TestContext.Current.CancellationToken));

        Assert.Empty(OutputEntries());
    }

    [Fact]
    public async Task Cancelling_leaves_nothing_behind()
    {
        Serve(Primary);
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress(p =>
        {
            if (p.Stage == JobStage.Downloading)
                cts.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Job().RunAsync(progress, cts.Token));

        Assert.Empty(OutputEntries());
    }

    [Fact]
    public async Task An_index_that_points_outside_the_mirror_is_refused()
    {
        // Four levels up from <out>/<work>/<set>/vdb64 is the test root. The file
        // is really served, so only the path check can stop the write.
        _mirror.Add($"{Primary}/version2.lst", $"+../../../../escape.vdb, {TestFiles.Sha256(BaseA)}, {BaseA.Length}\n");
        _mirror.Add("http://primary.mirror.test/escape.vdb", BaseA);

        await Assert.ThrowsAsync<UserFacingException>(() => Job().RunAsync(null, TestContext.Current.CancellationToken));

        Assert.False(File.Exists(Path.Combine(_files.Root, "escape.vdb")));
        Assert.Empty(OutputEntries());
    }

    [Fact]
    public void An_expired_license_is_refused_before_any_request()
    {
        var job = new MirrorJob(new MirrorOptions
        {
            KeyPath = _files.Write("old.key", TestFiles.Key("2020-01-01 (00:00) UTC")),
            ZonePath = _files.Write("zone.ini", TestFiles.UnixZone),
            OutputDirectory = _out,
        }, _mirror);

        Assert.Throws<UserFacingException>(() => job.Preflight());
        Assert.Empty(_mirror.Requests);
    }

    /// <summary>Progress&lt;T&gt; posts to the thread pool; tests need the callback inline.</summary>
    private sealed class SyncProgress(Action<JobProgress> onReport) : IProgress<JobProgress>
    {
        public void Report(JobProgress value) => onReport(value);
    }
}
