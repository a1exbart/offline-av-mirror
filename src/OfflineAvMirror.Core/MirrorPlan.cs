using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

/// <summary>A file to download: its place in the mirror and what it must be.</summary>
public sealed record PlannedFile(string RelativePath, Uri Url, string Sha256, long Size);

/// <summary>
/// Everything a mirror consists of, known before the first byte is written.
/// </summary>
/// <remarks>
/// The indexes are small and already downloaded and verified; the files are
/// what is left to fetch. Planning completely up front means an interrupted
/// run never leaves something that looks like a mirror but is half of one.
/// </remarks>
public sealed class MirrorPlan
{
    /// <summary>Top directory inside the output, named as the product expects it.</summary>
    public required string RootDirectory { get; init; }

    public required IReadOnlyList<(string RelativePath, byte[] Data)> Indexes { get; init; }

    public required IReadOnlyList<PlannedFile> Files { get; init; }

    public DateTime? Timestamp { get; init; }

    public long TotalBytes => Files.Sum(f => f.Size);

    /// <summary>
    /// Reject paths that would escape the mirror directory. They come from the
    /// network, and a hostile or broken mirror must not be able to write
    /// outside the output folder.
    /// </summary>
    public static string CheckRelativePath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        var segments = path.Split('/');
        var bad = path.Length == 0
                  || path.Contains('\\', StringComparison.Ordinal)
                  || path.Contains(':', StringComparison.Ordinal)
                  || path[0] == '/'
                  || Array.Exists(segments, s => s.Length == 0 || s == "." || s == "..")
                  || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0;
        if (bad)
        {
            throw new UserFacingException(
                T($"The mirror index contains an unsafe path: \"{path}\".",
                  $"Индекс зеркала содержит недопустимый путь: «{path}»."),
                T("Nothing was written. Report this if it repeats.",
                  "Ничего не записано. Если повторится — сообщите об этом."));
        }
        return path;
    }

    /// <summary>
    /// Try each mirror of the zone in order until one answers. A license
    /// refusal is final: every mirror would give the same answer.
    /// </summary>
    internal static async Task<(Uri BaseUrl, byte[] Data)> FetchFirstAsync(
        MirrorClient client, Zone zone, string entryName, CancellationToken ct)
    {
        UserFacingException? last = null;
        foreach (var baseUrl in zone.Urls)
        {
            try
            {
                return (baseUrl, await client.GetBytesAsync(Join(baseUrl, entryName), ct).ConfigureAwait(false));
            }
            catch (MirrorRefusedException)
            {
                throw;
            }
            catch (UserFacingException ex)
            {
                last = ex;
            }
        }
        throw last ?? new UserFacingException(T("No update mirror answered.", "Ни одно зеркало обновлений не ответило."));
    }

    internal static Uri Join(Uri baseUrl, string relativePath) =>
        new($"{baseUrl.AbsoluteUri.TrimEnd('/')}/{relativePath}");
}
