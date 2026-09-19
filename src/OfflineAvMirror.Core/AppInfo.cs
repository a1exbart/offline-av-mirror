using System.Reflection;

namespace OfflineAvMirror.Core;

public static class AppInfo
{
    public const string Name = "offline-av-mirror";

    /// <summary>
    /// Version stamped at build time from the git tag; source-link metadata
    /// after '+' is dropped.
    /// </summary>
    public static string Version { get; } = ReadVersion();

    private static string ReadVersion()
    {
        var info = typeof(AppInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrEmpty(info))
            return "0.0.0-dev";
        var plus = info.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? info : info[..plus];
    }
}
