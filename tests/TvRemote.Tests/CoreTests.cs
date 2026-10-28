using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Xml.Linq;
using SonyTvRemote;
using Xunit;

namespace SonyTvRemote.Tests
{
    public class AppInfoTests
    {
        [Fact]
        public void Version_comes_from_the_csproj()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "app", "TvRemote.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            var csproj = XDocument.Load(Path.Combine(dir.FullName, "app", "TvRemote.csproj"));
            var version = csproj.Descendants("Version").Single().Value;
            Assert.Equal(version, AppInfo.Version);
        }

        [Fact]
        public void Changelog_file_has_the_current_version()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CHANGELOG.md"))) dir = dir.Parent;
            Assert.NotNull(dir);
            Assert.Contains("## [" + AppInfo.Version + "]", File.ReadAllText(Path.Combine(dir.FullName, "CHANGELOG.md")));
            Assert.StartsWith(AppInfo.Version + " ", Strings.Get(Lang.EnUs, "ChangelogText"));
            Assert.StartsWith(AppInfo.Version + " ", Strings.Get(Lang.PtBr, "ChangelogText"));
        }
    }

    public class StringsTests
    {
        [Fact]
        public void Both_languages_have_the_same_keys()
        {
            var en = Strings.Table(Lang.EnUs).Keys.OrderBy(k => k).ToArray();
            var pt = Strings.Table(Lang.PtBr).Keys.OrderBy(k => k).ToArray();
            Assert.Equal(en, pt);
        }

        [Fact]
        public void No_value_is_empty()
        {
            foreach (var lang in new[] { Lang.EnUs, Lang.PtBr })
                foreach (var kv in Strings.Table(lang))
                    Assert.False(string.IsNullOrWhiteSpace(kv.Value), lang + ":" + kv.Key);
        }

        [Fact]
        public void Get_uses_the_requested_language_and_formats_arguments()
        {
            Assert.Equal("Not paired — click Pair with TV", Strings.Get(Lang.EnUs, "NotPaired"));
            Assert.Equal("Não pareado — clique em Parear com a TV", Strings.Get(Lang.PtBr, "NotPaired"));
            Assert.Equal("Failed: HTTP 500", Strings.Get(Lang.EnUs, "HttpFailed", 500));
        }

        [Fact]
        public void Language_codes_round_trip_and_default_to_english()
        {
            Assert.Equal("pt-BR", Strings.Code(Lang.PtBr));
            Assert.Equal(Lang.PtBr, Strings.FromCode("pt-BR"));
            Assert.Equal(Lang.EnUs, Strings.FromCode("en-US"));
            Assert.Equal(Lang.EnUs, Strings.FromCode(null));
            Assert.Equal(Lang.EnUs, Strings.FromCode("xx"));
        }

        [Fact]
        public void Changelog_lists_both_versions_in_both_languages()
        {
            foreach (var lang in new[] { Lang.EnUs, Lang.PtBr })
            {
                var text = Strings.Get(lang, "ChangelogText");
                Assert.Contains("1.1.0", text);
                Assert.Contains("1.0.0", text);
            }
        }
    }

    public class WakeOnLanTests
    {
        [Fact]
        public void Magic_packet_is_six_FF_then_the_mac_sixteen_times()
        {
            var p = WakeOnLan.BuildMagicPacket("01:23:45:67:89:AB");
            Assert.Equal(102, p.Length);
            Assert.All(p.Take(6), b => Assert.Equal(0xFF, b));
            var mac = new byte[] { 0x01, 0x23, 0x45, 0x67, 0x89, 0xAB };
            for (int i = 0; i < 16; i++)
                Assert.Equal(mac, p.Skip(6 + i * 6).Take(6).ToArray());
        }

        [Fact]
        public void Magic_packet_accepts_dashes()
            => Assert.Equal(WakeOnLan.BuildMagicPacket("01:23:45:67:89:AB"), WakeOnLan.BuildMagicPacket("01-23-45-67-89-ab"));

        [Theory]
        [InlineData("")]
        [InlineData("01:23:45:67:89")]
        [InlineData("01:23:45:67:89:ZZ")]
        public void Magic_packet_rejects_bad_mac(string mac)
            => Assert.Throws<FormatException>(() => WakeOnLan.BuildMagicPacket(mac));

        [Theory]
        [InlineData("192.168.1.100", true)]
        [InlineData("my-tv.local", true)]
        [InlineData("192.168.1. 100", false)]
        [InlineData("192.168.1.100:abc", false)]
        [InlineData("http://192.168.1.100", false)]
        public void Valid_tv_host(string ip, bool expected)
            => Assert.Equal(expected, UrlHelper.IsValidHost(ip));

        [Theory]
        [InlineData("192.168.1.100", 24, "192.168.1.255")]
        [InlineData("10.1.2.3", 16, "10.1.255.255")]
        [InlineData("172.16.5.9", 20, "172.16.15.255")]
        public void Subnet_broadcast(string ip, int prefix, string expected)
            => Assert.Equal(IPAddress.Parse(expected), WakeOnLan.Broadcast(IPAddress.Parse(ip), prefix));
    }

    public class UrlHelperTests
    {
        [Theory]
        [InlineData("example.com", "https://example.com")]
        [InlineData("  example.com/a?b=1 ", "https://example.com/a?b=1")]
        [InlineData("http://example.com", "http://example.com")]
        [InlineData("HTTPS://example.com", "HTTPS://example.com")]
        public void Adds_https_only_when_there_is_no_scheme(string input, string expected)
            => Assert.Equal(expected, UrlHelper.Normalize(input));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("https://")]
        [InlineData("http://")]
        public void Empty_input_gives_null(string input)
            => Assert.Null(UrlHelper.Normalize(input));
    }

    public class IrccCodesTests
    {
        const string Sample = "{\"id\":1,\"result\":[{\"bundled\":true,\"type\":\"RM-J1100\"},[{\"name\":\"Options\",\"value\":\"AAAAAgAAAJcAAAA2Aw==\"},{\"name\":\"Return\",\"value\":\"AAAAAgAAAJcAAAAjAw==\"}]]}";

        [Fact]
        public void Parses_the_tv_list_by_name()
        {
            var codes = IrccCodes.Parse(Sample);
            Assert.Equal("AAAAAgAAAJcAAAA2Aw==", codes["Options"]);
            Assert.Equal("AAAAAgAAAJcAAAAjAw==", codes["Return"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{\"id\":1,\"error\":[12,\"x\"]}")]
        public void Bad_reply_gives_null(string body)
            => Assert.Null(IrccCodes.Parse(body));

        [Fact]
        public void Fallback_has_the_official_codes_for_every_button()
        {
            var f = IrccCodes.Fallback;
            Assert.Equal("AAAAAgAAAJcAAAA2Aw==", f["Options"]);
            Assert.Equal("AAAAAgAAAJcAAAAjAw==", f["Return"]);
            Assert.Equal("AAAAAQAAAAEAAAA6Aw==", f["Display"]);
            foreach (var name in new[] { "Options", "Return", "Display", "Home", "Confirm", "Up", "Down", "Left", "Right",
                                         "VolumeUp", "VolumeDown", "Mute", "ChannelUp", "ChannelDown", "Input", "PowerOff" })
                Assert.True(f.ContainsKey(name), name);
        }

        [Fact]
        public void Resolve_prefers_the_tv_list_and_falls_back()
        {
            var tv = IrccCodes.Parse(Sample);
            Assert.Equal("AAAAAgAAAJcAAAA2Aw==", IrccCodes.Resolve(tv, "Options"));
            Assert.Equal(IrccCodes.Fallback["Home"], IrccCodes.Resolve(tv, "Home"));
            Assert.Equal(IrccCodes.Fallback["Home"], IrccCodes.Resolve(null, "Home"));
        }
    }

    public class ScalarReplyTests
    {
        [Theory]
        [InlineData(403, "{\"auth_url\":\"http://x/sony/accessControl\"}", true)]
        [InlineData(401, "", true)]
        [InlineData(200, "{\"error\":[403,\"Forbidden\"],\"id\":1}", true)]
        [InlineData(200, "{\"id\":1,\"auth_url\":\"x\"}", true)]
        [InlineData(200, "{\"id\":1,\"result\":[]}", false)]
        [InlineData(200, "{\"error\":[15,\"power on not supported\"],\"id\":1}", false)]
        public void Detects_auth_required(int status, string body, bool expected)
            => Assert.Equal(expected, ScalarReply.NeedsAuth(status, body));

        [Fact]
        public void Reads_error_code()
        {
            Assert.Equal(15, ScalarReply.ErrorCode("{\"error\":[15,\"power on not supported\"],\"id\":1}"));
            Assert.Null(ScalarReply.ErrorCode("{\"id\":1,\"result\":[]}"));
            Assert.Null(ScalarReply.ErrorCode("garbage"));
        }

        [Fact]
        public void Reads_power_status()
        {
            Assert.Equal("active", ScalarReply.PowerStatus("{\"id\":1,\"result\":[{\"status\":\"active\"}]}"));
            Assert.Equal("standby", ScalarReply.PowerStatus("{\"id\":1,\"result\":[{\"status\":\"standby\"}]}"));
            Assert.Null(ScalarReply.PowerStatus("{\"error\":[1,\"x\"]}"));
        }

        [Fact]
        public void Reads_wol_mac()
        {
            Assert.Equal("01:23:45:67:89:AB", ScalarReply.WolMac(
                "{\"id\":1,\"result\":[[{\"option\":\"WOL\",\"value\":\"01:23:45:67:89:AB\"},{\"option\":\"Other\",\"value\":\"no\"}]]}"));
            Assert.Null(ScalarReply.WolMac("{\"id\":1,\"result\":[[{\"option\":\"Other\",\"value\":\"no\"}]]}"));
        }
    }

    public class AuthCookieTests
    {
        static readonly DateTime Now = new DateTime(2026, 10, 28, 4, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Parses_value_and_expiry_from_max_age()
        {
            Assert.True(AuthCookie.TryParse("auth=abc123; path=/sony/; max-age=1209600; expires=Wed, 11-Nov-2026 04:17:44 GMT;",
                Now, out var cookie, out var expires));
            Assert.Equal("auth=abc123", cookie);
            Assert.Equal(Now.AddSeconds(1209600), expires);
        }

        [Fact]
        public void Falls_back_to_expires_attribute()
        {
            Assert.True(AuthCookie.TryParse("auth=abc; path=/sony/; expires=Wed, 11-Nov-2026 04:17:44 GMT",
                Now, out _, out var expires));
            Assert.Equal(new DateTime(2026, 11, 11, 4, 17, 44, DateTimeKind.Utc), expires);
        }

        [Fact]
        public void Absurd_max_age_does_not_throw()
        {
            Assert.True(AuthCookie.TryParse("auth=x; max-age=99999999999999", Now, out _, out var expires));
            Assert.True(expires > Now);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("other=1; path=/")]
        public void Rejects_headers_without_auth(string header)
            => Assert.False(AuthCookie.TryParse(header, Now, out _, out _));

        [Fact]
        public void Needs_renewal_under_two_days()
        {
            Assert.False(AuthCookie.NeedsRenewal(Now.AddDays(3), Now));
            Assert.True(AuthCookie.NeedsRenewal(Now.AddDays(1.9), Now));
            Assert.True(AuthCookie.NeedsRenewal(Now.AddDays(-1), Now));
        }
    }
}
