using Avalonia;
using Avalonia.Styling;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror;

public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Light, dark, or whatever the desktop says.
/// </summary>
/// <remarks>
/// The choice is stored in the user profile, not next to the program: the
/// program is often run from a USB drive, and a read-only drive must not turn a
/// cosmetic setting into a failure. The default is "system" — a window that
/// shines white on a dark desktop looks broken.
/// </remarks>
public static class ThemeSwitch
{
    private static readonly string StatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "offline-av-mirror", "theme");

    public static ThemeChoice Current { get; private set; } = ThemeChoice.System;

    public static void Restore()
    {
        try
        {
            if (File.Exists(StatePath) && Enum.TryParse<ThemeChoice>(File.ReadAllText(StatePath).Trim(), out var saved))
                Current = saved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Unreadable setting: stay with "system".
        }

        Apply();
    }

    public static void Next()
    {
        Current = Current switch
        {
            ThemeChoice.System => ThemeChoice.Light,
            ThemeChoice.Light => ThemeChoice.Dark,
            _ => ThemeChoice.System,
        };
        Apply();
        Save();
    }

    public static string Describe() => Current switch
    {
        ThemeChoice.Light => T("Light theme", "Светлая тема"),
        ThemeChoice.Dark => T("Dark theme", "Тёмная тема"),
        _ => T("Follow the system", "Как в системе"),
    };

    private static void Apply()
    {
        if (Application.Current is null)
            return;

        Application.Current.RequestedThemeVariant = Current switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StatePath)!);
            File.WriteAllText(StatePath, Current.ToString());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A theme preference has no right to end the program.
        }
    }
}
