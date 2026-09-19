using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

/// <summary>
/// A vendor license key file (<c>drweb32.key</c>), read locally.
/// </summary>
/// <remarks>
/// The update mirror admits a client that presents the MD5 of the key file.
/// The key itself never leaves this machine and is never copied into output.
/// </remarks>
public sealed class LicenseKey
{
    public const string DefaultFileName = "drweb32.key";

    public required string Path { get; init; }

    /// <summary>MD5 of the whole file, lowercase hex.</summary>
    public required string Md5 { get; init; }

    /// <summary>License number without leading zeros.</summary>
    public string Number { get; init; } = "";

    public string Owner { get; init; } = "";

    /// <summary>Expiry in UTC, or null when the key does not state one.</summary>
    public DateTime? Expires { get; init; }

    public bool IsExpiredAt(DateTime utcNow) => Expires is { } e && e < utcNow;

    public static LicenseKey Load(string path)
    {
        byte[] raw;
        try
        {
            raw = File.ReadAllBytes(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new UserFacingException(
                T($"Cannot read the license key: {path}", $"Не удалось прочитать ключ: {path}"),
                ex.Message, ex);
        }

        var ini = IniReader.Parse(Encoding.UTF8.GetString(raw));
        if (!ini.HasSection("Key"))
        {
            throw new UserFacingException(
                T("This file is not a license key (no [Key] section).",
                  "Этот файл не похож на лицензионный ключ (нет секции [Key])."),
                T($"Use the {DefaultFileName} file from the protected machine.",
                  $"Возьмите файл {DefaultFileName} с защищаемой машины."));
        }

        return new LicenseKey
        {
            Path = path,
            // Hash the bytes exactly as stored: any text normalisation would
            // change the hash and the mirror would reject the client.
#pragma warning disable CA5351 // MD5 is what the vendor protocol uses; it is not a security decision here.
            Md5 = Convert.ToHexStringLower(MD5.HashData(raw)),
#pragma warning restore CA5351
            Number = (ini.Get("User", "Number") ?? "").TrimStart('0'),
            Owner = ini.Get("User", "Name") ?? "",
            Expires = ParseExpires(ini),
        };
    }

    /// <summary>
    /// Keys carry a readable <c>Expires=2029-08-23 (16:23) UTC</c>, a numeric
    /// <c>EX=</c> epoch, or both, depending on when they were issued.
    /// </summary>
    internal static DateTime? ParseExpires(IniReader ini)
    {
        var text = ini.Get("Key", "Expires");
        if (!string.IsNullOrWhiteSpace(text)
            && DateTime.TryParseExact(text.Trim().Split(' ')[0], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        return long.TryParse(ini.Get("Key", "EX"), NumberStyles.None, CultureInfo.InvariantCulture, out var epoch) && epoch > 0
            ? DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime
            : null;
    }

    /// <summary>
    /// The key next to the program: <c>drweb32.key</c> first, otherwise the only
    /// <c>*.key</c> file. Two candidates mean the user has to choose.
    /// </summary>
    public static string? FindIn(string directory)
    {
        if (!Directory.Exists(directory))
            return null;

        var exact = System.IO.Path.Combine(directory, DefaultFileName);
        if (File.Exists(exact))
            return exact;

        var all = Directory.GetFiles(directory, "*.key");
        return all.Length == 1 ? all[0] : null;
    }
}
