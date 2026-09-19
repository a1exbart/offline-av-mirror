using System.Globalization;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

public sealed record MirrorOptions
{
    public required string KeyPath { get; init; }
    public required string ZonePath { get; init; }
    public required string OutputDirectory { get; init; }
    public bool CreateArchive { get; init; }
    public Uri? Proxy { get; init; }

    /// <summary>Free space required up front: the bases plus room for an archive.</summary>
    public long RequiredFreeMegabytes { get; init; } = 1500;
}

public enum JobStage
{
    Planning,
    Downloading,
    Packing,
}

public sealed record JobProgress(JobStage Stage, int FileIndex, int FileCount, string CurrentFile, long BytesDone, long BytesTotal);

public sealed record JobResult(string OutputPath, bool IsArchive, int Files, long Bytes, DateTime? BasesTimestamp);

/// <summary>
/// One complete run: key and zone, plan, download, then a folder or an archive.
/// </summary>
/// <remarks>
/// All or nothing. Work happens in a hidden temporary folder next to the output;
/// only a complete, verified set is moved into place, and every other outcome —
/// error, cancellation — removes it. Half a set looks exactly like a set, and
/// nobody should carry one to a machine that has no way to fetch the rest.
/// </remarks>
public sealed class MirrorJob(MirrorOptions options, HttpMessageHandler? handler = null)
{
    private const string WorkPrefix = ".offline-av-mirror-";

    public LicenseKey? Key { get; private set; }
    public Zone? Zone { get; private set; }

    /// <summary>
    /// Everything that can be checked before touching the network or the disk.
    /// </summary>
    public void Preflight(DateTime? utcNow = null)
    {
        Key = LicenseKey.Load(options.KeyPath);
        if (Key.IsExpiredAt(utcNow ?? DateTime.UtcNow))
        {
            throw new UserFacingException(
                T($"The license expired on {Key.Expires:yyyy-MM-dd}.", $"Лицензия истекла {Key.Expires:dd.MM.yyyy}."),
                T("The mirror admits only a valid license.", "Зеркало пускает только с действующей лицензией."));
        }

        Zone = Zone.Load(options.ZonePath);
        OutputFolder.Check(options.OutputDirectory, options.RequiredFreeMegabytes);
    }

    public async Task<JobResult> RunAsync(IProgress<JobProgress>? progress, CancellationToken ct)
    {
        if (Key is null || Zone is null)
            Preflight();

        OutputFolder.RemoveStale(options.OutputDirectory, WorkPrefix);
        var work = Path.Combine(options.OutputDirectory, WorkPrefix + Path.GetRandomFileName());
        try
        {
            var name = OutputFolder.UniqueName(options.OutputDirectory, SetName(Zone!.Kind, DateTime.Now));
            var set = Path.Combine(work, name);

            progress?.Report(new JobProgress(JobStage.Planning, 0, 0, "", 0, 0));
            using var client = new MirrorClient(Key!, options.Proxy, handler);
            var plan = Zone.Kind == ZoneKind.Windows
                ? await WindowsPlanner.PlanAsync(client, Zone, ct).ConfigureAwait(false)
                : await UnixPlanner.PlanAsync(client, Zone, ct).ConfigureAwait(false);

            var root = Path.Combine(set, plan.RootDirectory);
            foreach (var (relative, data) in plan.Indexes)
                await WriteAsync(Local(root, relative), data, ct).ConfigureAwait(false);

            long done = 0;
            for (var i = 0; i < plan.Files.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var f = plan.Files[i];
                progress?.Report(new JobProgress(JobStage.Downloading, i + 1, plan.Files.Count, f.RelativePath, done, plan.TotalBytes));
                await client.DownloadAsync(f.Url, Local(root, f.RelativePath), f.Sha256, f.Size,
                    n =>
                    {
                        done += n;
                        progress?.Report(new JobProgress(JobStage.Downloading, i + 1, plan.Files.Count, f.RelativePath, done, plan.TotalBytes));
                    }, ct).ConfigureAwait(false);
            }

            string output;
            if (options.CreateArchive)
            {
                progress?.Report(new JobProgress(JobStage.Packing, plan.Files.Count, plan.Files.Count, "", done, plan.TotalBytes));
                output = ArchiveWriter.Pack(set, Zone.Kind, options.OutputDirectory);
            }
            else
            {
                output = Path.Combine(options.OutputDirectory, name);
                Directory.Move(set, output);
            }

            return new JobResult(output, options.CreateArchive, plan.Files.Count, plan.TotalBytes, plan.Timestamp);
        }
        finally
        {
            OutputFolder.TryDelete(work);
        }
    }

    /// <summary><c>av-bases-unix-20260920-1530</c>: kind first, so two lines never share a name.</summary>
    internal static string SetName(ZoneKind kind, DateTime local) =>
        $"av-bases-{(kind == ZoneKind.Windows ? "windows" : "unix")}-{local.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}";

    private static string Local(string root, string relative) =>
        Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));

    private static async Task WriteAsync(string path, byte[] data, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, data, ct).ConfigureAwait(false);
    }
}
