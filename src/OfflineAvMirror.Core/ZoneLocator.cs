namespace OfflineAvMirror.Core;

/// <summary>Where a zone file came from. Shown to the user next to the zone.</summary>
public enum ZoneSource
{
    /// <summary>Chosen by the user (dialog or <c>--zone</c>).</summary>
    Chosen,

    /// <summary>Found next to the program.</summary>
    NextToProgram,

    /// <summary>Taken from the antivirus installed on this machine.</summary>
    InstalledAntivirus,
}

public sealed record ZoneCandidate(string Path, ZoneSource Source);

/// <summary>
/// Finds a zone file without asking, in a fixed order of trust.
/// </summary>
/// <remarks>
/// A file the user put next to the program wins over the antivirus installed on
/// this machine: the bases are usually meant for another machine, and a local
/// installation may well be on a different update line. Nothing is built in —
/// a default that ships with the program ages with the program, not with the
/// machine it is meant for.
/// </remarks>
public static class ZoneLocator
{
    /// <summary>Zone file of the Linux product, as installed by the vendor.</summary>
    public const string LinuxInstalledZone = "/var/opt/drweb.com/drl/bases/zone.ini";

    private static readonly string[] UnixNames = ["zone.ini", "bases.ini"];
    private static readonly string[] WindowsNames = ["drwzones.xml"];

    public static ZoneCandidate? Find(string programDirectory, ZoneKind kind)
    {
        var nearby = FindIn(programDirectory, kind);
        if (nearby is not null)
            return new ZoneCandidate(nearby, ZoneSource.NextToProgram);

        if (kind == ZoneKind.Unix && OperatingSystem.IsLinux() && Zone.KindOf(LinuxInstalledZone) == ZoneKind.Unix)
            return new ZoneCandidate(LinuxInstalledZone, ZoneSource.InstalledAntivirus);

        return null;
    }

    /// <summary>
    /// A zone file of the given kind in a directory. Conventional names are
    /// tried first, then any <c>*.ini</c>/<c>*.xml</c>; content decides.
    /// </summary>
    public static string? FindIn(string directory, ZoneKind kind)
    {
        if (!Directory.Exists(directory))
            return null;

        var names = kind == ZoneKind.Windows ? WindowsNames : UnixNames;
        var preferred = names.Select(n => Path.Combine(directory, n)).Where(File.Exists);
        var others = Directory.GetFiles(directory, "*.ini").Concat(Directory.GetFiles(directory, "*.xml"));

        return preferred.Concat(others).FirstOrDefault(p => Zone.KindOf(p) == kind);
    }
}
