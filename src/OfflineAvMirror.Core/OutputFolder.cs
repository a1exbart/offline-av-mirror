using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

/// <summary>Checks and housekeeping for the folder the mirror is written to.</summary>
internal static class OutputFolder
{
    /// <summary>
    /// The folder exists, accepts a file, and has room. Writability is tested
    /// by writing: on network shares and removable media the permission bits
    /// often say yes while the write fails.
    /// </summary>
    public static void Check(string directory, long requiredMegabytes)
    {
        if (!Directory.Exists(directory))
        {
            throw new UserFacingException(
                T($"The folder does not exist: {directory}", $"Папки не существует: {directory}"),
                T("Choose an existing folder.", "Выберите существующую папку."));
        }

        var probe = Path.Combine(directory, ".offline-av-mirror-write-test");
        try
        {
            File.WriteAllText(probe, "");
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UserFacingException(
                T($"Cannot write to the folder: {directory}", $"В папку нельзя записать: {directory}"),
                T("Choose a folder you can write to, for example in your home folder or on a USB drive.",
                  "Выберите папку, в которую у вас есть право записи, например в домашней папке или на флешке."), ex);
        }

        long freeMegabytes;
        try
        {
            freeMegabytes = new DriveInfo(Path.GetFullPath(directory)).AvailableFreeSpace / (1024 * 1024);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return; // The medium does not report its size; not a reason to refuse.
        }

        if (freeMegabytes < requiredMegabytes)
        {
            throw new UserFacingException(
                T($"Not enough free space: {freeMegabytes} MB available, {requiredMegabytes} MB needed.",
                  $"Мало места: свободно {freeMegabytes} МБ, нужно не меньше {requiredMegabytes} МБ."),
                T("Free some space or choose another drive.", "Освободите место или выберите другой носитель."));
        }
    }

    /// <summary>
    /// Remove work folders left by runs that were killed or lost power. Only
    /// our own prefix, and only older than two hours, so a second instance
    /// running right now keeps its work.
    /// </summary>
    public static void RemoveStale(string directory, string prefix)
    {
        try
        {
            foreach (var dir in Directory.GetDirectories(directory, prefix + "*"))
            {
                if (DateTime.UtcNow - Directory.GetCreationTimeUtc(dir) > TimeSpan.FromHours(2))
                    TryDelete(dir);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Housekeeping never blocks a download.
        }
    }

    /// <summary>A name not yet taken in the folder, as a file or as a directory.</summary>
    public static string UniqueName(string directory, string baseName)
    {
        bool Taken(string n) =>
            Directory.Exists(Path.Combine(directory, n))
            || File.Exists(Path.Combine(directory, n + ".tar.gz"))
            || File.Exists(Path.Combine(directory, n + ".zip"));

        var name = baseName;
        for (var i = 2; Taken(name); i++)
            name = $"{baseName}-{i}";
        return name;
    }

    public static void TryDelete(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover temporary folder must not turn a finished run into a failure;
            // RemoveStale picks it up next time.
        }
    }
}
