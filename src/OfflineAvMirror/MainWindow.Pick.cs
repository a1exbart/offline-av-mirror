using Avalonia.Controls;
using Avalonia.Platform.Storage;

using OfflineAvMirror.Core;

namespace OfflineAvMirror;

/// <summary>Choosing the key, the zone and the output folder.</summary>
public partial class MainWindow : Window
{
    private async Task PickKeyAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.PickKey,
            AllowMultiple = false,
            // "All files" is not decoration: keys arrive under names like
            // "company (3).key", and sometimes without an extension at all.
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.KeyFiles) { Patterns = ["*.key"] },
                new FilePickerFileType(Strings.AllFiles) { Patterns = ["*"] },
            ],
        });

        if (files.Count == 0)
            return;

        _keyPath = files[0].Path.LocalPath;
        _keyChosenByHand = true;
        Refresh();
    }

    private async Task PickZoneAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.PickZone,
            AllowMultiple = false,
            // Both extensions: zone.ini for Linux, drwzones.xml for Windows.
            // A filter for one of them hides the other line entirely.
            FileTypeFilter =
            [
                new FilePickerFileType(Strings.ZoneFiles) { Patterns = ["*.ini", "*.xml"] },
                new FilePickerFileType(Strings.AllFiles) { Patterns = ["*"] },
            ],
        });

        if (files.Count == 0)
            return;

        var picked = files[0].Path.LocalPath;
        // The file goes to the line it actually belongs to, and the window
        // switches to that line: the person showed what they want.
        var kind = Zone.KindOf(picked) ?? _line;
        _zones[kind] = new ZoneCandidate(picked, ZoneSource.Chosen);
        SetLine(kind);
    }

    private async Task PickOutputAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Strings.PickOut,
            AllowMultiple = false,
        });

        if (folders.Count == 0)
            return;

        _outDir = folders[0].Path.LocalPath;
        Refresh();
    }
}
