using System.Net;

namespace OfflineAvMirror.Core.Tests;

/// <summary>An in-memory update mirror: URL → bytes, plus a record of what was asked.</summary>
internal sealed class FakeMirror : HttpMessageHandler
{
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
    private readonly HashSet<string> _down = new(StringComparer.OrdinalIgnoreCase);

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>Key MD5 the mirror admits; null admits everyone.</summary>
    public string? AdmittedKeyMd5 { get; set; }

    public void Add(string url, byte[] data) => _files[url] = data;

    public void Add(string url, string text) => Add(url, System.Text.Encoding.UTF8.GetBytes(text));

    /// <summary>Make a host refuse connections.</summary>
    public void TakeDown(string host) => _down.Add(host);

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var url = request.RequestUri!;

        if (_down.Contains(url.Host))
            throw new HttpRequestException("connection refused");

        if (AdmittedKeyMd5 is not null
            && (!request.Headers.TryGetValues("X-DrWeb-Validate", out var v) || v.Single() != AdmittedKeyMd5))
        {
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)451));
        }

        return Task.FromResult(_files.TryGetValue(url.AbsoluteUri, out var data)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) }
            : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
