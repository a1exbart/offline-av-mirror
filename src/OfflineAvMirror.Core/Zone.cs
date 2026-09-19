using System.Xml;
using System.Xml.Linq;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

/// <summary>Which product line a zone serves. The two are laid out differently.</summary>
public enum ZoneKind
{
    /// <summary>Linux/Unix products: a flat <c>version2.lst</c> index, plain files.</summary>
    Unix,

    /// <summary>Windows products: a tree of XML indexes, files as <c>.lzma</c> only.</summary>
    Windows,
}

/// <summary>
/// An update zone: the mirror addresses and the path of one update line.
/// </summary>
/// <remarks>
/// The zone belongs to the antivirus build on the machine being updated, so it is
/// always read from that machine's own zone file (<c>zone.ini</c> on Linux,
/// <c>drwzones.xml</c> on Windows) and never guessed. Both files are signed by
/// the vendor; they are read, never modified or redistributed.
/// </remarks>
public sealed class Zone
{
    public required string FilePath { get; init; }

    public required ZoneKind Kind { get; init; }

    /// <summary>Primary mirror first, then fallbacks, in file order.</summary>
    public required IReadOnlyList<Uri> Urls { get; init; }

    /// <summary>Line path without scheme and host, e.g. <c>unix/…/vdb64</c>.</summary>
    public required string Path { get; init; }

    /// <summary>Last path segment; the Unix mirror keeps its files under it.</summary>
    public string DirName => Path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "bases";

    public static Zone Load(string path)
    {
        string text;
        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UserFacingException(
                T($"Cannot read the zone file: {path}", $"Не удалось прочитать файл зоны: {path}"),
                ex.Message, ex);
        }

        // Decide by content, not by name: the file comes from another machine
        // and may have been renamed on the way.
        return text.Contains("<zones", StringComparison.OrdinalIgnoreCase)
            ? LoadXml(path, text)
            : LoadIni(path, text);
    }

    /// <summary>The kind of a zone file, or null when it is not a zone file at all.</summary>
    public static ZoneKind? KindOf(string path)
    {
        try
        {
            return Load(path).Kind;
        }
        catch (UserFacingException)
        {
            return null;
        }
    }

    /// <summary><c>http://host/unix/x/vdb64/</c> → <c>unix/x/vdb64</c>.</summary>
    public static string PathFromUrl(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);
        return url.AbsolutePath.Trim('/');
    }

    private static Zone LoadIni(string path, string text)
    {
        var ini = IniReader.Parse(text);
        var urls = ini.GetAll("DRL", "url").Concat(ini.GetAll("FALLBACK", "url"))
                      .Select(u => Uri.TryCreate(u.TrimEnd('/'), UriKind.Absolute, out var uri) ? uri : null)
                      .OfType<Uri>()
                      .ToList();

        if (urls.Count == 0)
            throw NotAZone(path);

        return new Zone { FilePath = path, Kind = ZoneKind.Unix, Urls = urls, Path = PathFromUrl(urls[0]) };
    }

    private static Zone LoadXml(string path, string text)
    {
        XElement root;
        try
        {
            root = XDocument.Parse(text).Root ?? throw NotAZone(path);
        }
        catch (XmlException)
        {
            throw NotAZone(path);
        }

        var linePath = (root.Element("path")?.Attribute("value")?.Value
                        ?? root.Element("geo-update")?.Attribute("path")?.Value
                        ?? "").Trim('/');
        if (linePath.Length == 0)
            throw NotAZone(path);

        var scheme = root.Element("protocol")?.Attribute("value")?.Value ?? "http";

        // Same order as the vendor client: the geo server by name, then the
        // numbered mirrors.
        var urls = root.Elements("geo-update").Elements("server").Select(e => e.Attribute("address")?.Value)
                       .Concat(root.Elements("zone").Select(e => e.Attribute("address")?.Value))
                       .Where(h => !string.IsNullOrWhiteSpace(h))
                       .Select(h => Uri.TryCreate($"{scheme}://{h!.Trim()}/{linePath}", UriKind.Absolute, out var u) ? u : null)
                       .OfType<Uri>()
                       .ToList();
        if (urls.Count == 0)
            throw NotAZone(path);

        return new Zone { FilePath = path, Kind = ZoneKind.Windows, Urls = urls, Path = linePath };
    }

    private static UserFacingException NotAZone(string path) => new(
        T($"Not a zone file, or it lists no mirrors: {path}",
          $"Это не файл зоны или в нём нет ни одного адреса зеркала: {path}"),
        T("Copy the zone file from the protected machine again (see README).",
          "Скопируйте файл зоны с защищаемой машины заново (см. README)."));
}
