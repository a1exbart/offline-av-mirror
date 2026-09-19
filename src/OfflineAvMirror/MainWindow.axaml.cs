using Avalonia.Controls;
using Avalonia.Input;

using OfflineAvMirror.Core;

namespace OfflineAvMirror;

/// <summary>
/// The window: three things have to be ready before anything can start.
/// </summary>
/// <remarks>
/// The action button stays disabled until the key, the zone and the output
/// folder are all in place, and what is missing is spelled out on screen. A
/// refusal that arrives ten minutes into a download is one the person cannot
/// explain to themselves.
/// </remarks>
public partial class MainWindow : Window
{
    private readonly Dictionary<ZoneKind, ZoneCandidate> _zones = [];
    private ZoneKind _line = ZoneKind.Unix;
    private string? _keyPath;
    private string? _outDir;
    private bool _keyChosenByHand;
    private Action? _cancelRun;

    public MainWindow()
    {
        InitializeComponent();

        AboutButton.Click += (_, _) => new AboutWindow().ShowDialog(this);
        ThemeButton.Click += (_, _) =>
        {
            ThemeSwitch.Next();
            ShowTheme();
        };
        UnixCard.PointerPressed += (_, _) => SetLine(ZoneKind.Unix);
        WindowsCard.PointerPressed += (_, _) => SetLine(ZoneKind.Windows);
        // Segments are not buttons, so without this they would respond to the
        // mouse only — a dead end on a machine without one.
        UnixCard.KeyDown += (_, e) => ChooseWithKeyboard(e, ZoneKind.Unix);
        WindowsCard.KeyDown += (_, e) => ChooseWithKeyboard(e, ZoneKind.Windows);
        KeyBrowse.Click += async (_, _) => await PickKeyAsync();
        ZoneBrowse.Click += async (_, _) => await PickZoneAsync();
        OutBrowse.Click += async (_, _) => await PickOutputAsync();
        MainButton.Click += async (_, _) => await MainActionAsync();
        CancelButton.Click += (_, _) => _cancelRun?.Invoke();
        CopyShaButton.Click += async (_, _) => await CopyChecksumAsync();
        OpenFolderButton.Click += (_, _) => OpenResultFolder();

        ToolTip.SetTip(ThemeButton, Strings.ThemeTip);
        ShowTheme();

        var here = AppContext.BaseDirectory;
        _keyPath = LicenseKey.FindIn(here) ?? LicenseKey.FindIn(Directory.GetCurrentDirectory());
        // Look for both lines up front: switching between them must not send
        // the user into a file dialog.
        foreach (var kind in new[] { ZoneKind.Unix, ZoneKind.Windows })
        {
            if (ZoneLocator.Find(here, kind) is { } found)
                _zones[kind] = found;
        }
        _outDir = Directory.Exists(here) ? here : Directory.GetCurrentDirectory();

        SetLine(ZoneKind.Unix);
    }

    private void SetLine(ZoneKind line)
    {
        _line = line;
        Paint(UnixCard, line == ZoneKind.Unix);
        Paint(WindowsCard, line == ZoneKind.Windows);
        Refresh();
    }

    private static void Paint(Border card, bool selected)
    {
        if (selected)
            card.Classes.Add("on");
        else
            card.Classes.Remove("on");
    }

    private void ChooseWithKeyboard(KeyEventArgs e, ZoneKind line)
    {
        if (e.Key is not (Key.Space or Key.Enter))
            return;
        SetLine(line);
        e.Handled = true;
    }

    private void ShowTheme()
    {
        IconSun.IsVisible = ThemeSwitch.Current == ThemeChoice.Light;
        IconMoon.IsVisible = ThemeSwitch.Current == ThemeChoice.Dark;
        IconAuto.IsVisible = ThemeSwitch.Current == ThemeChoice.System;
        ToolTip.SetTip(ThemeButton, ThemeSwitch.Describe());
    }

    /// <summary>Re-read the three conditions and say which one is missing.</summary>
    private void Refresh()
    {
        var blocked = DescribeKey() ?? DescribeZone();
        OutValue.Text = _outDir ?? "—";

        BlockReason.Text = blocked ?? "";
        BlockReason.IsVisible = blocked is not null;
        MainButton.IsEnabled = blocked is null && _outDir is not null;
    }

    private string? DescribeKey()
    {
        KeyTerm.Text = KeyOwner.Text = KeySourceText.Text = "";

        if (_keyPath is null)
        {
            KeyValue.Text = Strings.NotFound;
            return Strings.NoKey;
        }

        try
        {
            var key = LicenseKey.Load(_keyPath);
            KeyValue.Text = key.Number.Length > 0 ? $"№ {key.Number}" : Path.GetFileName(_keyPath);
            KeyTerm.Text = Strings.LicenseExpires(key.Expires);
            KeyOwner.Text = key.Owner;
            KeySourceText.Text = $"{Strings.Source}: "
                + (_keyChosenByHand ? Strings.SourceChosen : Strings.SourceNextToProgram)
                + $" ({Path.GetFileName(_keyPath)})";
            return key.IsExpiredAt(DateTime.UtcNow) ? Strings.LicenseExpires(key.Expires) : null;
        }
        catch (UserFacingException ex)
        {
            KeyValue.Text = Path.GetFileName(_keyPath);
            return ex.Message;
        }
    }

    private string? DescribeZone()
    {
        if (!_zones.TryGetValue(_line, out var candidate))
        {
            ZoneValue.Text = Strings.NotFound;
            ZoneSourceText.Text = "";
            return _line == ZoneKind.Windows ? Strings.NoZoneWindows : Strings.NoZoneUnix;
        }

        try
        {
            var zone = Zone.Load(candidate.Path);
            ZoneValue.Text = zone.Path;
            ZoneSourceText.Text = $"{Strings.Source}: {Strings.SourceOf(candidate.Source)} ({Path.GetFileName(candidate.Path)})";
            // Belt and braces: each line keeps its own file, so this is reachable
            // only if the file changed under us.
            return zone.Kind == _line ? null : Strings.WrongLine;
        }
        catch (UserFacingException ex)
        {
            ZoneValue.Text = Path.GetFileName(candidate.Path);
            ZoneSourceText.Text = "";
            return ex.Message;
        }
    }
}
