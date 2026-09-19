using OfflineAvMirror.Core;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror;

/// <summary>
/// The same job without a window.
/// </summary>
/// <remarks>
/// Anything the program can do has to be doable from a command line: that is
/// what makes it scriptable, and what lets someone reproduce a problem over the
/// phone. Exit codes: 0 done, 1 failed, 2 wrong usage, 130 interrupted.
/// </remarks>
internal static class Cli
{
    public static int Run(string[] args)
    {
        try
        {
            switch (args[0])
            {
                case "--help" or "-h":
                    Console.WriteLine(Usage());
                    return 0;
                case "--version" or "-V":
                    Console.WriteLine($"{AppInfo.Name} {AppInfo.Version}");
                    return 0;
                case "download":
                    return Download(Parse(args));
                default:
                    Console.Error.WriteLine(T($"Unknown command: {args[0]}", $"Неизвестная команда: {args[0]}"));
                    Console.Error.WriteLine(Usage());
                    return 2;
            }
        }
        catch (UsageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }
        catch (UserFacingException ex)
        {
            Console.Error.WriteLine(T("Failed: ", "Отказ: ") + ex.Message);
            if (ex.Hint is { } hint)
                Console.Error.WriteLine("  " + hint);
            return 1;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine(T("Interrupted. Nothing was kept.", "Прервано. Ничего не сохранено."));
            return 130;
        }
    }

    private static int Download(MirrorOptions options)
    {
        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;   // Clean up after ourselves instead of dying where we stand.
            cts.Cancel();
        };

        var job = new MirrorJob(options);
        job.Preflight();
        Console.WriteLine($"{AppInfo.Name} {AppInfo.Version}");
        Console.WriteLine(T($"License:  No. {job.Key!.Number}, {Strings.LicenseExpires(job.Key.Expires)}",
                            $"Лицензия: № {job.Key!.Number}, {Strings.LicenseExpires(job.Key.Expires)}"));
        Console.WriteLine(T($"Zone:     {job.Zone!.Path}", $"Зона:     {job.Zone!.Path}"));
        Console.WriteLine(T($"Output:   {options.OutputDirectory}", $"Каталог:  {options.OutputDirectory}"));

        var lastFile = "";
        var progress = new SynchronousProgress(p =>
        {
            if (p.Stage != JobStage.Downloading || p.CurrentFile == lastFile)
                return;
            lastFile = p.CurrentFile;
            Console.WriteLine($"  [{p.FileIndex}/{p.FileCount}] {p.CurrentFile}");
        });

        var result = job.RunAsync(progress, cts.Token).GetAwaiter().GetResult();

        Console.WriteLine(T($"Done: {result.OutputPath}", $"Готово: {result.OutputPath}"));
        Console.WriteLine($"{Strings.HumanSize(result.Bytes)} · {Strings.FilesCount(result.Files)}");
        return 0;
    }

    private static MirrorOptions Parse(string[] args)
    {
        string? key = null, zone = null, output = null, proxy = null;
        var kind = ZoneKind.Unix;
        var archive = false;

        for (var i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--key": key = Value(args, ++i); break;
                case "--zone": zone = Value(args, ++i); break;
                case "--out": output = Value(args, ++i); break;
                case "--proxy": proxy = Value(args, ++i); break;
                case "--archive": archive = true; break;
                case "--line":
                    kind = Value(args, ++i).StartsWith("win", StringComparison.OrdinalIgnoreCase)
                        ? ZoneKind.Windows : ZoneKind.Unix;
                    break;
                default:
                    throw new UsageException(T($"Unknown option: {args[i]}", $"Неизвестный параметр: {args[i]}"));
            }
        }

        var here = AppContext.BaseDirectory;
        var cwd = Directory.GetCurrentDirectory();

        key ??= LicenseKey.FindIn(cwd) ?? LicenseKey.FindIn(here)
            ?? throw new UsageException(T(
                $"No license key found next to the program or in this folder. Use --key.",
                $"Лицензионный ключ не найден ни рядом с программой, ни в этой папке. Укажите --key."));

        zone ??= (ZoneLocator.Find(cwd, kind) ?? ZoneLocator.Find(here, kind))?.Path
            ?? throw new UsageException(T(
                "No zone file found. Copy it from the machine you are updating and pass it with --zone.",
                "Файл зоны не найден. Скопируйте его с обновляемой машины и укажите через --zone."));

        return new MirrorOptions
        {
            KeyPath = key,
            ZonePath = zone,
            OutputDirectory = output ?? cwd,
            CreateArchive = archive,
            Proxy = proxy is null ? null : new Uri(proxy.Contains("://", StringComparison.Ordinal) ? proxy : "http://" + proxy),
        };
    }

    private static string Value(string[] args, int index) => index < args.Length
        ? args[index]
        : throw new UsageException(T($"Option {args[index - 1]} needs a value.", $"Параметру {args[index - 1]} нужно значение."));

    private static string Usage() => T($"""
        {AppInfo.Name} {AppInfo.Version} — offline mirror of antivirus virus bases

          {AppInfo.Name}                     open the window
          {AppInfo.Name} download [options]  download without a window

        Options:
          --out DIR      where to put the result (default: current folder)
          --key FILE     license key (default: drweb32.key next to the program)
          --zone FILE    zone file from the machine being updated
          --line unix|windows   which line to look for when --zone is omitted (default: unix)
          --archive      pack the result into one archive with a .sha256 file
          --proxy HOST:PORT     use an HTTP proxy
          --help, --version
        """, $"""
        {AppInfo.Name} {AppInfo.Version} — офлайн-зеркало вирусных баз антивируса

          {AppInfo.Name}                     открыть окно
          {AppInfo.Name} download [параметры] скачать без окон

        Параметры:
          --out КАТАЛОГ  куда положить результат (по умолчанию — текущая папка)
          --key ФАЙЛ     лицензионный ключ (по умолчанию — drweb32.key рядом с программой)
          --zone ФАЙЛ    файл зоны с обновляемой машины
          --line unix|windows   какую линию искать, если --zone не задан (по умолчанию unix)
          --archive      упаковать результат в один архив с файлом .sha256
          --proxy ХОСТ:ПОРТ     ходить через HTTP-прокси
          --help, --version
        """);

    private sealed class UsageException(string message) : Exception(message);

    /// <summary>Progress&lt;T&gt; hops to the thread pool; console output stays in order.</summary>
    private sealed class SynchronousProgress(Action<JobProgress> report) : IProgress<JobProgress>
    {
        public void Report(JobProgress value) => report(value);
    }
}
