using System.Security.Cryptography;
using System.Text;

namespace OfflineAvMirror.Core.Tests;

/// <summary>
/// Synthetic inputs. Nothing here comes from a real key or a real zone: hosts
/// are on the reserved <c>.test</c> domain and the key is made up.
/// </summary>
internal sealed class TestFiles : IDisposable
{
    public TestFiles()
    {
        Root = Path.Combine(Path.GetTempPath(), "oam-tests-" + Path.GetRandomFileName());
        Directory.CreateDirectory(Root);
    }

    public string Root { get; }

    public const string UnixZone = """
        [DRL]
        url=http://primary.mirror.test/unix/line1/1110/vdb64
        [FALLBACK]
        url=http://fallback1.mirror.test/unix/line1/1110/vdb64
        url=http://fallback2.mirror.test/unix/line1/1110/vdb64
        """;

    public const string WindowsZone = """
        <?xml version="1.0" encoding="utf-8"?>
        <zones version="1.0">
          <geo-update path="/xmlzone/line1/release/1220/windows">
            <server address="geo.mirror.test" name="geo"/>
          </geo-update>
          <path value="/xmlzone/line1/release/1220/windows"/>
          <protocol value="http"/>
          <zone address="zone1.mirror.test" name="zone1"/>
        </zones>
        """;

    public static string Key(string expires = "2099-01-01 (00:00) UTC") => $"""
        [Key]
        Expires={expires}
        [User]
        Number=0000123456
        Name=Test Owner
        """;

    public string Write(string name, string content)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    public string Dir(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public static string Sha256(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data));

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort in a temp folder.
        }
    }
}
