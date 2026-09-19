using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

/// <summary>
/// Plans a Windows-line mirror of the virus bases group (<c>vdb</c>).
/// </summary>
/// <remarks>
/// <code>
/// vdb-versions.xml                      entry point of the group
///   └─ &lt;version&gt;/vdb-revision.xml      list of component indexes with SHA-256
///        └─ 90/av-engine/9/revision.xml
///             └─ &lt;file name=… hash=… size=…&gt;&lt;lzma name=… hash=… size=…/&gt;&lt;/file&gt;
/// </code>
/// Only the compressed variant is stored: the vendor's mirror holds nothing
/// else and the product expects exactly that. Program components (the
/// <c>comps</c> group) are deliberately left out — they would upgrade the
/// antivirus itself, which is a separate decision for whoever runs it.
/// </remarks>
public static class WindowsPlanner
{
    /// <summary>The directory name the vendor client gives its mirror.</summary>
    public const string RootDirectory = "Dr.Web Update Mirror";

    public const string GroupEntry = "vdb-versions.xml";

    public static async Task<MirrorPlan> PlanAsync(MirrorClient client, Zone zone, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(zone);

        var (baseUrl, entry) = await MirrorPlan.FetchFirstAsync(client, zone, GroupEntry, ct).ConfigureAwait(false);
        var indexes = new List<(string, byte[])> { (GroupEntry, entry) };
        var files = new List<PlannedFile>();

        var versions = ParseXml(entry, GroupEntry);
        var version = versions.Element("version");
        var versionName = version?.Attribute("name")?.Value;
        if (string.IsNullOrWhiteSpace(versionName))
            throw Unexpected(GroupEntry);

        await CollectAsync(client, baseUrl, versions, "", indexes, files, ct).ConfigureAwait(false);

        var revisionName = MirrorPlan.CheckRelativePath($"{versionName}/vdb-revision.xml");
        var revision = await client.GetBytesAsync(MirrorPlan.Join(baseUrl, revisionName), ct).ConfigureAwait(false);
        indexes.Add((revisionName, revision));

        foreach (var x in ParseXml(revision, revisionName).Elements("xml"))
        {
            var name = x.Attribute("name")?.Value;
            if (string.IsNullOrWhiteSpace(name))
                continue;
            name = MirrorPlan.CheckRelativePath(name);

            var data = await client.GetBytesAsync(MirrorPlan.Join(baseUrl, name), ct).ConfigureAwait(false);
            VerifyIndex(data, name, x.Attribute("hash")?.Value);
            indexes.Add((name, data));

            // File paths in a component index are relative to that index.
            var slash = name.LastIndexOf('/');
            var dir = slash < 0 ? "" : name[..slash];
            await CollectAsync(client, baseUrl, ParseXml(data, name), dir, indexes, files, ct).ConfigureAwait(false);
        }

        if (files.Count == 0)
            throw Unexpected(revisionName);

        return new MirrorPlan
        {
            RootDirectory = RootDirectory,
            Indexes = indexes,
            Files = files,
            Timestamp = ParseTimestamp(version?.Element("timestamp")?.Attribute("value")?.Value),
        };
    }

    private static async Task CollectAsync(MirrorClient client, Uri baseUrl, XElement root, string dir,
        List<(string, byte[])> indexes, List<PlannedFile> files, CancellationToken ct)
    {
        foreach (var x in root.Elements("xml"))
        {
            var name = Combine(dir, x.Attribute("name")?.Value);
            if (name is null || indexes.Exists(i => i.Item1 == name))
                continue;
            var data = await client.GetBytesAsync(MirrorPlan.Join(baseUrl, name), ct).ConfigureAwait(false);
            VerifyIndex(data, name, x.Attribute("hash")?.Value);
            indexes.Add((name, data));
        }

        foreach (var f in root.Elements("file"))
        {
            var source = f.Element("lzma") ?? f;
            var name = Combine(dir, source.Attribute("name")?.Value);
            if (name is null)
                continue;
            _ = long.TryParse(source.Attribute("size")?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var size);
            files.Add(new PlannedFile(name, MirrorPlan.Join(baseUrl, name), source.Attribute("hash")?.Value ?? "", size));
        }
    }

    private static string? Combine(string dir, string? name) =>
        string.IsNullOrWhiteSpace(name) ? null : MirrorPlan.CheckRelativePath(dir.Length == 0 ? name : $"{dir}/{name}");

    private static XElement ParseXml(byte[] data, string name)
    {
        try
        {
            return XDocument.Parse(Encoding.UTF8.GetString(data)).Root ?? throw Unexpected(name);
        }
        catch (XmlException)
        {
            throw Unexpected(name);
        }
    }

    private static void VerifyIndex(byte[] data, string name, string? expected)
    {
        if (string.IsNullOrEmpty(expected))
            return;
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(data)), expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new UserFacingException(
                T($"Checksum mismatch: {name}.", $"Не сошлась контрольная сумма: {name}."),
                T("The file was damaged or altered in transit. Run the download again.",
                  "Файл повреждён или подменён при передаче. Запустите скачивание ещё раз."));
        }
    }

    /// <summary>
    /// The group timestamp looks like <c>20260831223816</c>. Its time zone is not
    /// stated anywhere, so it is kept as published.
    /// </summary>
    private static DateTime? ParseTimestamp(string? value) =>
        DateTime.TryParseExact(value, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var t) ? t : null;

    private static UserFacingException Unexpected(string name) => new(
        T($"The mirror returned an unexpected index: {name}.", $"Зеркало вернуло неожиданный индекс: {name}."),
        T("Check that the zone file comes from a Windows product, or try again later.",
          "Проверьте, что файл зоны взят у продукта для Windows, или повторите позже."));
}
