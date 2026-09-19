using System.Globalization;
using System.Text;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

/// <summary>A file the Unix index says must be in the mirror.</summary>
public sealed record UnixIndexEntry(string Name, string Sha256, long Size);

/// <summary>
/// The Unix line index, <c>version2.lst</c>.
/// </summary>
/// <remarks>
/// <code>
/// [DrWebUpdateList]
/// [1788209114]                                   set timestamp (epoch)
/// +drw11000.vdb, &lt;sha256&gt;, &lt;size&gt;
/// +drweb32.dll, &lt;sha256&gt;, &lt;size&gt;, &lt;sha256&gt;, &lt;size&gt;   also offered as .lzma
/// -dwr900*.*,                                    remove locally
/// [DrWebUpdateListEnd]
/// [Signature]
/// </code>
/// Only the plain variant of each file is fetched; that is what the vendor's own
/// mirror tool stores. The signature cannot be verified (the vendor's public key
/// is not published), so integrity rests on the SHA-256 and size of every file.
/// </remarks>
public sealed class UnixIndex
{
    public const string FileName = "version2.lst";

    public required IReadOnlyList<UnixIndexEntry> Entries { get; init; }

    /// <summary>The index exactly as received; it is stored in the mirror unchanged.</summary>
    public required ReadOnlyMemory<byte> Raw { get; init; }

    public DateTime? Timestamp { get; init; }

    public long TotalBytes => Entries.Sum(e => e.Size);

    public static UnixIndex Parse(ReadOnlyMemory<byte> raw)
    {
        var entries = new List<UnixIndexEntry>();
        DateTime? timestamp = null;

        foreach (var rawLine in Encoding.UTF8.GetString(raw.Span).Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
                continue;

            if (line[0] == '[')
            {
                if (long.TryParse(line.Trim('[', ']'), NumberStyles.None, CultureInfo.InvariantCulture, out var epoch))
                    timestamp = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                continue;
            }

            if (line[0] != '+')
                continue;

            // Fewer than three fields means a broken index. Skipping the line
            // would produce a mirror that is silently incomplete.
            var parts = line[1..].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length < 3 || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var size))
                throw BadIndex(line);

            entries.Add(new UnixIndexEntry(parts[0], parts[1].ToLowerInvariant(), size));
        }

        if (entries.Count == 0)
            throw BadIndex(T("(no entries)", "(нет ни одной записи)"));

        return new UnixIndex { Entries = entries, Raw = raw, Timestamp = timestamp };
    }

    private static UserFacingException BadIndex(string detail) => new(
        T($"The mirror returned an index that cannot be read: {detail}",
          $"Зеркало вернуло индекс, который не удаётся разобрать: {detail}"),
        T("Try again later.", "Повторите позже."));
}
