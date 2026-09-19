using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

using static OfflineAvMirror.Core.Localization;

namespace OfflineAvMirror.Core;

/// <summary>
/// HTTP access to the vendor update mirror.
/// </summary>
/// <remarks>
/// The mirror serves files only to a client that sends
/// <c>X-DrWeb-Validate: &lt;MD5 of the license key file&gt;</c>; anything else gets
/// HTTP 451. The other headers do not affect admission but are sent anyway, so
/// the vendor sees an honest client. Plain HTTP is what the zone files specify;
/// integrity comes from the SHA-256 and size of every file, not from transport.
/// </remarks>
public sealed class MirrorClient : IDisposable
{
    private const int UnavailableForLegalReasons = 451;

    private readonly HttpClient _http;

    public MirrorClient(LicenseKey key, Uri? proxy = null, HttpMessageHandler? handler = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        handler ??= new SocketsHttpHandler
        {
            Proxy = proxy is null ? null : new WebProxy(proxy),
            UseProxy = proxy is not null,
            AutomaticDecompression = DecompressionMethods.None,
        };
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(10) };

        var h = _http.DefaultRequestHeaders;
        h.TryAddWithoutValidation("User-Agent", $"offline-av-mirror/{AppInfo.Version}");
        h.TryAddWithoutValidation("X-DrWeb-Validate", key.Md5);
        if (key.Number.Length > 0)
            h.TryAddWithoutValidation("X-DrWeb-KeyNumber", key.Number);
        h.TryAddWithoutValidation("X-DrWeb-SysHash", SystemHash());
        h.TryAddWithoutValidation("Accept", "*/*");
    }

    /// <summary>Fetch a small file (an index) into memory.</summary>
    public async Task<byte[]> GetBytesAsync(Uri url, CancellationToken ct)
    {
        using var response = await SendAsync(url, ct).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Download a file, verify size and SHA-256, and only then give it its final
    /// name. A file that failed verification never sits under the right name.
    /// </summary>
    public async Task DownloadAsync(Uri url, string destination, string sha256, long size,
        Action<long>? bytesRead, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var partial = destination + ".part";

        using (var response = await SendAsync(url, ct).ConfigureAwait(false))
        {
            var net = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using (net.ConfigureAwait(false))
            {
                var file = File.Create(partial);
                await using (file.ConfigureAwait(false))
                {
                    var buffer = new byte[128 * 1024];
                    int read;
                    while ((read = await net.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
                        bytesRead?.Invoke(read);
                    }
                }
            }
        }

        Verify(partial, Path.GetFileName(destination), sha256, size);
        File.Move(partial, destination, overwrite: true);
    }

    internal static void Verify(string path, string shownName, string sha256, long size)
    {
        var actualSize = new FileInfo(path).Length;
        if (size > 0 && actualSize != size)
        {
            File.Delete(path);
            throw new UserFacingException(
                T($"Incomplete download: {shownName} ({actualSize} of {size} bytes).",
                  $"Файл скачан не полностью: {shownName} ({actualSize} из {size} байт)."),
                T("The connection probably dropped. Run the download again.",
                  "Скорее всего, оборвалась связь. Запустите скачивание ещё раз."));
        }

        if (sha256.Length == 0)
            return;

        string actual;
        using (var stream = File.OpenRead(path))
            actual = Convert.ToHexStringLower(SHA256.HashData(stream));

        if (!string.Equals(actual, sha256, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new UserFacingException(
                T($"Checksum mismatch: {shownName}.", $"Не сошлась контрольная сумма: {shownName}."),
                T("The file was damaged or altered in transit. Run the download again.",
                  "Файл повреждён или подменён при передаче. Запустите скачивание ещё раз."));
        }
    }

    private async Task<HttpResponseMessage> SendAsync(Uri url, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new UserFacingException(
                T($"No connection to the update mirror ({url.Host}).", $"Нет связи с зеркалом обновлений ({url.Host})."),
                T("Check the internet connection. If it goes through a proxy, specify it.",
                  "Проверьте доступ в интернет. Если он идёт через прокси, укажите его."), ex);
        }

        if (response.IsSuccessStatusCode)
            return response;

        var code = (int)response.StatusCode;
        response.Dispose();
        throw code switch
        {
            UnavailableForLegalReasons => new MirrorRefusedException(),
            404 => new UserFacingException(
                T($"The mirror has no such file: {url}", $"На зеркале нет файла: {url}"),
                T("The zone file probably points to a retired update line. Copy it again from the protected machine.",
                  "Вероятно, файл зоны указывает на снятую линию обновлений. Скопируйте его заново с защищаемой машины.")),
            _ => new UserFacingException(
                T($"The mirror refused the request (HTTP {code}).", $"Зеркало отказало в запросе (HTTP {code})."),
                T("Try again later.", "Повторите позже.")),
        };
    }

    /// <summary>
    /// The vendor derives this from TPM, the root filesystem UUID or a MAC
    /// address. It does not affect admission, so a MAC-based hash is enough.
    /// </summary>
    private static string SystemHash()
    {
        var mac = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => n.GetPhysicalAddress().ToString())
            .FirstOrDefault(s => s.Length > 0);

#pragma warning disable CA5351 // Mirrors the vendor client's identifier format, not a security control.
        return mac is null ? new string('0', 32) : Convert.ToHexString(MD5.HashData(Encoding.ASCII.GetBytes(mac)));
#pragma warning restore CA5351
    }

    public void Dispose() => _http.Dispose();
}

/// <summary>
/// HTTP 451: the mirror did not accept the license. Every mirror gives the same
/// answer, so callers stop instead of trying the next one.
/// </summary>
public sealed class MirrorRefusedException : UserFacingException
{
    public MirrorRefusedException()
        : base(
            T("The update mirror did not accept the license key (HTTP 451).",
              "Зеркало обновлений не приняло лицензионный ключ (HTTP 451)."),
            T("Use a valid key from the protected machine; the mirror checks it on every request.",
              "Нужен действующий ключ с защищаемой машины: зеркало проверяет его при каждом запросе."))
    {
    }

    public MirrorRefusedException(string message)
        : base(message)
    {
    }

    public MirrorRefusedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
