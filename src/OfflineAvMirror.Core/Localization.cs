using System.Runtime.InteropServices;

namespace OfflineAvMirror.Core;

public enum UiLanguage
{
    English,
    Russian,
}

/// <summary>
/// Two languages, picked once at startup.
/// </summary>
/// <remarks>
/// The app is built with invariant globalization (smaller single file, no ICU
/// dependency on minimal Linux installs), so <c>CultureInfo.CurrentUICulture</c>
/// is always invariant and satellite resources would never load. The language
/// is therefore read from the environment directly.
/// </remarks>
public static partial class Localization
{
    public const string OverrideVariable = "OFFLINE_AV_MIRROR_LANG";

    public static UiLanguage Current { get; set; } = Detect();

    /// <summary>Pick the string for the current language.</summary>
    public static string T(string english, string russian) =>
        Current == UiLanguage.Russian ? russian : english;

    public static UiLanguage Detect()
    {
        var forced = Environment.GetEnvironmentVariable(OverrideVariable);
        if (!string.IsNullOrWhiteSpace(forced))
            return FromTag(forced);

        if (OperatingSystem.IsWindows())
            return (GetUserDefaultUILanguage() & 0x3FF) == LangRussian ? UiLanguage.Russian : UiLanguage.English;

        // POSIX precedence: LC_ALL overrides LC_MESSAGES, which overrides LANG.
        foreach (var name in new[] { "LC_ALL", "LC_MESSAGES", "LANG" })
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
                return FromTag(value);
        }
        return UiLanguage.English;
    }

    internal static UiLanguage FromTag(string tag) =>
        tag.Trim().StartsWith("ru", StringComparison.OrdinalIgnoreCase) ? UiLanguage.Russian : UiLanguage.English;

    private const int LangRussian = 0x19;

    [LibraryImport("kernel32.dll")]
    private static partial ushort GetUserDefaultUILanguage();
}
