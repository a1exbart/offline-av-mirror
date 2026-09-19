using System.Security.Cryptography;
using System.Text;

namespace OfflineAvMirror.Core.Tests;

public sealed class ParsingTests : IDisposable
{
    private readonly TestFiles _files = new();

    public void Dispose() => _files.Dispose();

    [Fact]
    public void Ini_keeps_repeated_keys_in_order()
    {
        var ini = IniReader.Parse("[A]\nurl=1\n; comment\nurl=2\n[B]\nurl=3\n");

        Assert.Equal(["1", "2"], ini.GetAll("a", "URL"));
        Assert.True(ini.HasSection("B"));
    }

    [Fact]
    public void Key_hash_is_taken_over_the_raw_bytes()
    {
        var text = TestFiles.Key().Replace("\n", "\r\n", StringComparison.Ordinal);
        var path = _files.Write("drweb32.key", text);
#pragma warning disable CA5351
        var expected = Convert.ToHexStringLower(MD5.HashData(File.ReadAllBytes(path)));
#pragma warning restore CA5351

        var key = LicenseKey.Load(path);

        Assert.Equal(expected, key.Md5);
        Assert.Equal("123456", key.Number);
        Assert.Equal("Test Owner", key.Owner);
        Assert.Equal(new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc), key.Expires);
    }

    [Fact]
    public void Key_expiry_falls_back_to_the_epoch_field()
    {
        var ini = IniReader.Parse("[Key]\nEX=1700000000\n");

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, LicenseKey.ParseExpires(ini));
    }

    [Fact]
    public void A_file_without_a_key_section_is_rejected()
    {
        var path = _files.Write("other.key", "[Something]\na=b\n");

        Assert.Throws<UserFacingException>(() => LicenseKey.Load(path));
    }

    [Fact]
    public void Unix_zone_lists_primary_then_fallbacks()
    {
        var zone = Zone.Load(_files.Write("renamed.txt", TestFiles.UnixZone));

        Assert.Equal(ZoneKind.Unix, zone.Kind);
        Assert.Equal(3, zone.Urls.Count);
        Assert.Equal("primary.mirror.test", zone.Urls[0].Host);
        Assert.Equal("unix/line1/1110/vdb64", zone.Path);
        Assert.Equal("vdb64", zone.DirName);
    }

    [Fact]
    public void Windows_zone_is_recognised_by_content()
    {
        var zone = Zone.Load(_files.Write("zone.ini", TestFiles.WindowsZone));

        Assert.Equal(ZoneKind.Windows, zone.Kind);
        Assert.Equal("xmlzone/line1/release/1220/windows", zone.Path);
        Assert.Equal(["geo.mirror.test", "zone1.mirror.test"], zone.Urls.Select(u => u.Host));
    }

    [Theory]
    [InlineData("")]
    [InlineData("[DRL]\n")]
    [InlineData("<zones><broken")]
    public void Anything_else_is_not_a_zone(string content)
    {
        Assert.Null(Zone.KindOf(_files.Write("x.ini", content)));
    }

    [Fact]
    public void Unix_index_reads_entries_and_timestamp()
    {
        var raw = Encoding.UTF8.GetBytes(
            "[DrWebUpdateList]\r\n[1700000000]\r\n+a.vdb, ABCDEF, 10\r\n+b.dll, 0123, 20, 4567, 5\r\n-old*.*,\r\n[DrWebUpdateListEnd]\r\n");

        var index = UnixIndex.Parse(raw);

        Assert.Equal(2, index.Entries.Count);
        Assert.Equal(new UnixIndexEntry("a.vdb", "abcdef", 10), index.Entries[0]);
        Assert.Equal(30, index.TotalBytes);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000).UtcDateTime, index.Timestamp);
    }

    [Theory]
    [InlineData("+a.vdb, abc\n")]
    [InlineData("+a.vdb, abc, notanumber\n")]
    [InlineData("[DrWebUpdateList]\n[DrWebUpdateListEnd]\n")]
    public void A_broken_or_empty_index_is_an_error(string text)
    {
        Assert.Throws<UserFacingException>(() => UnixIndex.Parse(Encoding.UTF8.GetBytes(text)));
    }

    [Theory]
    [InlineData("../evil")]
    [InlineData("a/../../evil")]
    [InlineData("/etc/passwd")]
    [InlineData("a\\b")]
    [InlineData("C:x")]
    [InlineData("a//b")]
    [InlineData("")]
    public void Paths_from_the_network_cannot_escape_the_mirror(string path)
    {
        Assert.Throws<UserFacingException>(() => MirrorPlan.CheckRelativePath(path));
    }

    [Theory]
    [InlineData("drw11000.vdb")]
    [InlineData("90/av-engine/9/common/drw12000.vdb.lzma")]
    public void Ordinary_paths_pass(string path)
    {
        Assert.Equal(path, MirrorPlan.CheckRelativePath(path));
    }

    [Theory]
    [InlineData("ru_RU.UTF-8", UiLanguage.Russian)]
    [InlineData("ru", UiLanguage.Russian)]
    [InlineData("en_US.UTF-8", UiLanguage.English)]
    [InlineData("C.UTF-8", UiLanguage.English)]
    public void Language_tag_is_mapped(string tag, UiLanguage expected)
    {
        Assert.Equal(expected, Localization.FromTag(tag));
    }
}
