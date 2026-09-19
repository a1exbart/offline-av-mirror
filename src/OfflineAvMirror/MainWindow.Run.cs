using System.Diagnostics;

using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Threading;

using OfflineAvMirror.Core;

namespace OfflineAvMirror;

/// <summary>What happens after the button: progress, result, failure.</summary>
public partial class MainWindow : Window
{
    /// <summary>One button: "Download" before the run, "Close" after it.</summary>
    private async Task MainActionAsync()
    {
        if (DonePanel.IsVisible || FailPanel.IsVisible)
        {
            Close();
            return;
        }

        await RunAsync();
    }

    private async Task RunAsync()
    {
        var job = new MirrorJob(new MirrorOptions
        {
            KeyPath = _keyPath!,
            ZonePath = _zones[_line].Path,
            OutputDirectory = _outDir!,
            CreateArchive = ArchiveBox.IsChecked == true,
        });

        ShowWorking();
        using var cts = new CancellationTokenSource();
        _cancelRun = cts.Cancel;
        var progress = new Progress<JobProgress>(ShowProgress);

        try
        {
            job.Preflight();
            var result = await Task.Run(() => job.RunAsync(progress, cts.Token), cts.Token);
            ShowResult(result);
        }
        catch (OperationCanceledException)
        {
            ShowFailure(Strings.Stopped, Strings.StoppedHint);
        }
        catch (UserFacingException ex)
        {
            ShowFailure(ex.Message, ex.Hint);
        }
        finally
        {
            _cancelRun = null;
        }
    }

    private void ShowProgress(JobProgress p) => Dispatcher.UIThread.Post(() =>
    {
        WorkFile.Text = p.Stage switch
        {
            JobStage.Planning => Strings.Planning,
            JobStage.Packing => Strings.Packing,
            _ => p.CurrentFile,
        };
        WorkCount.Text = p.FileCount > 0
            ? $"{Strings.FileOf(p.FileIndex, p.FileCount)} · {Strings.HumanSize(p.BytesDone)} / {Strings.HumanSize(p.BytesTotal)}"
            : "";
        WorkBar.IsIndeterminate = p.BytesTotal == 0;
        WorkBar.Value = p.BytesTotal > 0 ? Math.Min(100, p.BytesDone * 100.0 / p.BytesTotal) : 0;
    });

    private void ShowWorking()
    {
        ReadyPanel.IsVisible = DonePanel.IsVisible = FailPanel.IsVisible = false;
        WorkPanel.IsVisible = CancelButton.IsVisible = true;
        MainButton.IsEnabled = false;
        WorkBar.IsIndeterminate = true;
        WorkFile.Text = Strings.Planning;
        WorkCount.Text = "";
    }

    private void ShowResult(JobResult result)
    {
        WorkPanel.IsVisible = CancelButton.IsVisible = false;
        DonePanel.IsVisible = true;
        DonePath.Text = result.OutputPath;
        DoneSize.Text = $"{Strings.HumanSize(result.Bytes)} · {Strings.FilesCount(result.Files)}";

        // A folder has no single checksum; the archive carries one next to it.
        ChecksumBlock.IsVisible = CopyShaButton.IsVisible = result.IsArchive;
        DoneSha.Text = result.IsArchive ? ReadChecksum(result.OutputPath) : "";

        MainButton.Content = Strings.Close;
        MainButton.IsEnabled = true;
    }

    private void ShowFailure(string reason, string? hint)
    {
        WorkPanel.IsVisible = CancelButton.IsVisible = false;
        FailPanel.IsVisible = true;
        FailReason.Text = reason;
        FailHint.Text = hint ?? "";
        FailHint.IsVisible = hint is not null;
        MainButton.Content = Strings.Close;
        MainButton.IsEnabled = true;
    }

    private static string ReadChecksum(string archive)
    {
        try
        {
            return File.ReadAllText(archive + ".sha256").Split(' ')[0];
        }
        catch (IOException)
        {
            return "";
        }
    }

    private async Task CopyChecksumAsync()
    {
        if (Clipboard is { } clipboard && DoneSha.Text is { Length: > 0 } text)
            await clipboard.SetTextAsync(text);
    }

    private void OpenResultFolder()
    {
        var path = DonePath.Text;
        var folder = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? _outDir;
        if (folder is null)
            return;

        try
        {
            using var _ = Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            // No file manager to open it with; the path is on screen and can be copied.
        }
    }
}
