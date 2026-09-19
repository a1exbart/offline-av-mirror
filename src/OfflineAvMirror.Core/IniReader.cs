namespace OfflineAvMirror.Core;

/// <summary>
/// Minimal INI reader that keeps order and repeated keys.
/// </summary>
/// <remarks>
/// Vendor zone files repeat <c>url=</c> a dozen times under <c>[FALLBACK]</c>.
/// A dictionary-based parser would silently keep one of them, and mirrors must
/// be tried in the order the vendor lists them.
/// </remarks>
public sealed class IniReader
{
    private readonly List<(string Section, string Key, string Value)> _items = [];

    public static IniReader Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var ini = new IniReader();
        var section = "";
        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line[0] is ';' or '#')
                continue;

            if (line[0] == '[' && line[^1] == ']')
            {
                section = line[1..^1].Trim();
                ini._items.Add((section, "", ""));
                continue;
            }

            var eq = line.IndexOf('=', StringComparison.Ordinal);
            if (eq <= 0)
                continue;
            ini._items.Add((section, line[..eq].Trim(), line[(eq + 1)..].Trim()));
        }
        return ini;
    }

    public bool HasSection(string section) =>
        _items.Exists(i => string.Equals(i.Section, section, StringComparison.OrdinalIgnoreCase));

    public string? Get(string section, string key) => GetAll(section, key).FirstOrDefault();

    public IEnumerable<string> GetAll(string section, string key) =>
        _items.Where(i => string.Equals(i.Section, section, StringComparison.OrdinalIgnoreCase)
                          && string.Equals(i.Key, key, StringComparison.OrdinalIgnoreCase))
              .Select(i => i.Value);
}
