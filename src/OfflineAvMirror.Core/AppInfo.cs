using System.Reflection;

namespace OfflineAvMirror.Core;

/// <summary>
/// Who this program says it is.
/// </summary>
/// <remarks>
/// The version is never written into a file: the build takes it from the git
/// tag and stamps it into the assembly, and everything that shows a version
/// reads it from here — the window, the command line and the User-Agent sent to
/// the mirror. A build that came from no tag says so instead of pretending.
/// </remarks>
public static class AppInfo
{
    public const string Name = "offline-av-mirror";

    /// <summary>The version alone: <c>1.2.3</c>, or <c>0.0.0-dev</c> for a local build.</summary>
    public static string Version { get; } = Split().Version;

    /// <summary>
    /// The commit the binary was built from, when the build recorded one.
    /// Empty otherwise. This is what identifies a file someone found on a USB
    /// drive months later.
    /// </summary>
    public static string Build { get; } = Split().Build;

    /// <summary>Version and commit in one line, for a window or a log.</summary>
    public static string FullVersion => Build.Length > 0 ? $"{Version} ({Build})" : Version;

    private static (string Version, string Build) Split() => Parse(
        typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>
    /// InformationalVersion is "&lt;version&gt;+&lt;build metadata&gt;", and CI puts the
    /// commit in the metadata.
    /// </summary>
    internal static (string Version, string Build) Parse(string? informationalVersion)
    {
        if (string.IsNullOrEmpty(informationalVersion))
            return ("0.0.0-dev", "");

        var plus = informationalVersion.IndexOf('+', StringComparison.Ordinal);
        return plus < 0
            ? (informationalVersion, "")
            : (informationalVersion[..plus], informationalVersion[(plus + 1)..]);
    }
}
