using OfflineAvMirror.Core;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror;

/// <summary>
/// Window texts in both languages.
/// </summary>
/// <remarks>
/// Static properties rather than resource files: the release is a trimmed
/// single file built with invariant globalization, where satellite assemblies
/// would never be loaded. XAML binds these with <c>{x:Static}</c>.
/// </remarks>
public static class Strings
{
    public static string AppTitle => T("Offline AV Mirror", "Offline AV Mirror");

    public static string WindowTitle => T("Offline AV Mirror — virus bases", "Offline AV Mirror — вирусные базы");

    public static string Headline => T("Download virus bases", "Загрузка вирусных баз");

    public static string Subtitle => T(
        "Downloads updates on this machine so they can be carried to a machine without internet access.",
        "Скачивает обновления на этой машине, чтобы перенести их на машину без доступа в интернет.");

    public static string About => T("About", "О программе");

    public static string ThemeTip => T("Change the theme", "Сменить тему оформления");

    public static string TargetSystem => T("Bases for which system", "Базы для какой системы");

    public static string LinuxLine => T("Linux", "Linux");

    public static string WindowsLine => T("Windows", "Windows");

    public static string LicenseKey => T("License key", "Лицензионный ключ");

    public static string UpdateLine => T("Update line (zone)", "Линия обновлений (зона)");

    public static string OutputFolder => T("Where to save", "Куда сохранить");

    public static string Browse => T("Choose…", "Выбрать…");

    public static string PackIntoArchive => T(
        "Pack into one archive with a checksum",
        "Упаковать в один архив с контрольной суммой");

    public static string Start => T("Download", "Скачать");

    public static string Close => T("Close", "Закрыть");

    public static string Cancel => T("Stop", "Прервать");

    public static string Working => T("Downloading", "Идёт скачивание");

    public static string Planning => T("Reading the list of files…", "Читается список файлов…");

    public static string Packing => T("Packing the archive…", "Упаковка архива…");

    public static string Done => T("Bases downloaded", "Базы скачаны");

    public static string Failed => T("Did not work out", "Не удалось");

    public static string ResultPath => T("Result", "Результат");

    public static string ResultSize => T("Size and contents", "Размер и состав");

    public static string ResultChecksum => T("SHA-256 — verify on the other machine", "SHA-256 — сверить на целевой машине");

    public static string CopyChecksum => T("Copy SHA-256", "Копировать SHA-256");

    public static string OpenFolder => T("Open folder", "Открыть папку");

    public static string Searching => T("looking next to the program…", "ищется рядом с программой…");

    public static string NotFound => T("not found", "не найдено");

    public static string Source => T("Source", "Источник");

    public static string SourceChosen => T("chosen by you", "выбран вами");

    public static string SourceNextToProgram => T("file next to the program", "файл рядом с программой");

    public static string SourceInstalled => T("the antivirus on this machine", "антивирус этой машины");

    public static string NoKey => T(
        "No license key. Put the key file (*.key) from the machine you are updating next to this program. "
        + "The key is never built into the program and never leaves this machine.",
        "Нет лицензионного ключа. Положите рядом с программой файл лицензии (*.key) с той машины, которую обновляете. "
        + "Ключ в программу не встроен и эту машину не покидает.");

    public static string NoZoneUnix => T(
        $"No zone file for Linux. Copy {ZoneLocator.LinuxInstalledZone} from the machine you are updating.",
        $"Нет файла зоны для Linux. Скопируйте {ZoneLocator.LinuxInstalledZone} с машины, которую обновляете.");

    public static string NoZoneWindows => T(
        "No zone file for Windows. Copy drwzones.xml from the machine you are updating.",
        "Нет файла зоны для Windows. Скопируйте drwzones.xml с машины, которую обновляете.");

    public static string WrongLine => T(
        "This file belongs to the other update line.",
        "Этот файл относится к другой линии обновлений.");

    public static string Stopped => T("Download stopped.", "Скачивание прервано.");

    public static string StoppedHint => T(
        "Nothing was kept: an unfinished set is removed completely.",
        "Ничего не сохранено: незавершённый набор удаляется целиком.");

    public static string PickKey => T("License key file", "Файл лицензионного ключа");

    public static string PickZone => T("Zone file from the machine you are updating", "Файл зоны с обновляемой машины");

    public static string PickOut => T("Where to save the bases", "Куда сохранить базы");

    public static string AllFiles => T("All files", "Все файлы");

    public static string ZoneFiles => T("Zone file (zone.ini, drwzones.xml)", "Файл зоны (zone.ini, drwzones.xml)");

    public static string KeyFiles => T("License key", "Лицензионный ключ");

    public static string Version => T($"Version {AppInfo.Version}", $"Версия {AppInfo.Version}");

    public static string AboutWhatItDoes => T("WHAT IT DOES", "ЧТО ДЕЛАЕТ");

    public static string AboutWhatItDoesText => T(
        "Downloads virus bases from the vendor's update mirror using your own license and lays them out "
        + "as a mirror folder, so an offline machine can take them with its usual update-from-folder procedure. "
        + "Both product lines are supported: Linux and Windows.",
        "Скачивает вирусные базы с зеркала обновлений вендора по вашей лицензии и раскладывает их папкой-зеркалом, "
        + "которую офлайновая машина принимает своей штатной процедурой обновления из папки. "
        + "Поддерживаются обе линии: Linux и Windows.");

    public static string AboutWhatItDoesNot => T("WHAT IT DOES NOT DO", "ЧЕГО НЕ ДЕЛАЕТ");

    public static string AboutWhatItDoesNotText => T(
        "Does not install or update an antivirus, does not change any settings, and does not touch the antivirus "
        + "on this machine. It only downloads files.",
        "Не устанавливает и не обновляет антивирус, не меняет настройки и не трогает антивирус на этой машине. "
        + "Программа только скачивает файлы.");

    public static string AboutKeyTitle => T("LICENSE KEY", "ЛИЦЕНЗИОННЫЙ КЛЮЧ");

    public static string AboutKeyText => T(
        "The key is read on this machine and is never copied into the result. Only its checksum is sent to the "
        + "mirror — without it the mirror serves nothing. The key itself is never sent anywhere and never logged.",
        "Ключ читается на этой машине и в результат не копируется. На зеркало передаётся только его контрольная "
        + "сумма — без неё зеркало ничего не отдаёт. Сам ключ никуда не отправляется и в журнал не пишется.");

    public static string AboutIntegrity => T("INTEGRITY", "ЦЕЛОСТНОСТЬ");

    public static string AboutIntegrityText => T(
        "Every file is checked against the checksum and size declared by the mirror index. An incomplete set never "
        + "becomes a result: if the download breaks or is stopped, what was fetched is removed.",
        "Каждый файл сверяется с контрольной суммой и размером из индекса зеркала. Неполный набор результатом не "
        + "становится: при обрыве или отмене скачанное удаляется.");

    public static string Trademark => T(
        "Dr.Web is a trademark of Doctor Web, Ltd. This program is not a Doctor Web product and is not affiliated "
        + "with or endorsed by them.",
        "Dr.Web — товарный знак ООО «Доктор Веб». Эта программа не является продуктом «Доктор Веб» и не связана с ним.");

    public static string LicenseExpires(DateTime? expires) => expires is null
        ? T("no expiry date in the key", "срок действия в ключе не указан")
        : T($"valid until {expires:yyyy-MM-dd}", $"действует до {expires:dd.MM.yyyy}");

    public static string FilesCount(int files) => T($"files in the set: {files}", $"файлов в наборе: {files}");

    public static string FileOf(int index, int count) => T($"file {index} of {count}", $"файл {index} из {count}");

    public static string HumanSize(long bytes) => bytes switch
    {
        >= 1L << 30 => T($"{bytes / (double)(1L << 30):0.#} GB", $"{bytes / (double)(1L << 30):0.#} ГБ"),
        >= 1L << 20 => T($"{bytes / (double)(1L << 20):0} MB", $"{bytes / (double)(1L << 20):0} МБ"),
        >= 1L << 10 => T($"{bytes / (double)(1L << 10):0} KB", $"{bytes / (double)(1L << 10):0} КБ"),
        _ => T($"{bytes} B", $"{bytes} Б"),
    };

    public static string SourceOf(ZoneSource source) => source switch
    {
        ZoneSource.NextToProgram => SourceNextToProgram,
        ZoneSource.InstalledAntivirus => SourceInstalled,
        _ => SourceChosen,
    };
}
