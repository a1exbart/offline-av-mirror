using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace OfflineAvMirror.Core;

/// <summary>
/// Packs a finished mirror folder into one file with a <c>.sha256</c> next to it.
/// </summary>
/// <remarks>
/// The Unix line goes into <c>.tar.gz</c> (it is unpacked on Linux), the
/// Windows line into an uncompressed <c>.zip</c>: its files are already LZMA,
/// and compressing them again costs minutes for a fraction of a percent.
/// Inside, everything sits under the folder name, so unpacking never spills
/// files into the current directory.
/// </remarks>
public static class ArchiveWriter
{
    public static string Pack(string folder, ZoneKind kind, string outDirectory)
    {
        ArgumentNullException.ThrowIfNull(folder);
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder));

        string archive;
        if (kind == ZoneKind.Unix)
        {
            archive = Path.Combine(outDirectory, name + ".tar.gz");
            using var file = File.Create(archive);
            using var gzip = new GZipStream(file, CompressionLevel.Optimal);
            TarFile.CreateFromDirectory(folder, gzip, includeBaseDirectory: true);
        }
        else
        {
            archive = Path.Combine(outDirectory, name + ".zip");
            ZipFile.CreateFromDirectory(folder, archive, CompressionLevel.NoCompression, includeBaseDirectory: true);
        }

        WriteChecksum(archive);
        return archive;
    }

    /// <summary>
    /// <c>sha256sum</c> format: hash, two spaces, bare file name, LF. A Windows
    /// path or CRLF would break <c>sha256sum -c</c> on the protected machine.
    /// </summary>
    public static string WriteChecksum(string file)
    {
        string hash;
        using (var stream = File.OpenRead(file))
            hash = Convert.ToHexStringLower(SHA256.HashData(stream));

        File.WriteAllText(file + ".sha256", $"{hash}  {Path.GetFileName(file)}\n", new UTF8Encoding(false));
        return hash;
    }
}
