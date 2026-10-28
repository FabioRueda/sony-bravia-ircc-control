using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using SonyTvRemote;
using Xunit;

namespace SonyTvRemote.Tests
{
    public class SettingsTests : IDisposable
    {
        private const string TvA = "192.168.1.100";
        private const string TvB = "192.168.1.101";
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "SonyTvRemoteTests-" + Guid.NewGuid());
        private string FilePath => Path.Combine(_dir, "settings.json");

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Fact]
        public void Missing_file_gives_defaults()
        {
            var s = Settings.Load(FilePath);
            Assert.Equal("192.168.1.100", s.Ip);
            Assert.Equal("en-US", s.Language);
            Assert.Null(s.GetCookie(s.Ip));
            Assert.Null(s.GetMac(s.Ip));
        }

        [Fact]
        public void Corrupt_file_gives_defaults()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath, "{ not json");
            Assert.Equal("192.168.1.100", Settings.Load(FilePath).Ip);
        }

        [Fact]
        public void Round_trip_keeps_values_and_hides_the_cookie()
        {
            if (!OperatingSystem.IsWindows()) return;   // DPAPI is Windows-only
            var s = Settings.Load(FilePath);
            s.Ip = "192.168.1.50";
            s.Language = "pt-BR";
            s.SetMac("192.168.1.50", "01:23:45:67:89:AB");
            var id = s.EnsureClientId();
            var exp = new DateTime(2026, 11, 11, 4, 0, 0, DateTimeKind.Utc);
            s.SetCookie("192.168.1.50", "auth=secretvalue", exp);
            s.Save(FilePath);

            Assert.DoesNotContain("secretvalue", File.ReadAllText(FilePath));

            var r = Settings.Load(FilePath);
            Assert.Equal("192.168.1.50", r.Ip);
            Assert.Equal("pt-BR", r.Language);
            Assert.Equal("01:23:45:67:89:AB", r.GetMac("192.168.1.50"));
            Assert.Equal(id, r.EnsureClientId());
            Assert.Equal("auth=secretvalue", r.GetCookie("192.168.1.50"));
            Assert.Equal(exp, r.GetAuthExpiresUtc("192.168.1.50"));
        }

        [Fact]
        public void Client_id_is_generated_once()
        {
            var s = Settings.Load(FilePath);
            var a = s.EnsureClientId();
            Assert.Equal(a, s.EnsureClientId());
            Assert.True(Guid.TryParse(a, out _));
        }

        [Fact]
        public void Clear_cookie_removes_it()
        {
            var s = Settings.Load(FilePath);
            s.SetCookie(TvA, "auth=x", DateTime.UtcNow.AddDays(5));
            s.ClearCookie(TvA);
            Assert.Null(s.GetCookie(TvA));
            Assert.Null(s.GetAuthExpiresUtc(TvA));
        }

        [Fact]
        public void Unreadable_protected_cookie_is_treated_as_missing()
        {
            var s = Settings.Load(FilePath);
            s.Entry(TvA).AuthCookieProtected = "bm90IGRwYXBp";
            Assert.Null(s.GetCookie(TvA));
        }

        // ---------- per-TV data (1.1.1) ----------

        [Fact]
        public void Another_ip_does_not_see_the_mac_or_cookie_of_the_first()
        {
            var s = Settings.Load(FilePath);
            s.SetMac(TvA, "01:23:45:67:89:AB");
            s.SetCookie(TvA, "auth=a", DateTime.UtcNow.AddDays(5));

            Assert.Null(s.GetMac(TvB));
            Assert.Null(s.GetCookie(TvB));
            Assert.Null(s.GetAuthExpiresUtc(TvB));
        }

        [Fact]
        public void Each_tv_keeps_its_own_data()
        {
            var s = Settings.Load(FilePath);
            s.SetMac(TvA, "01:23:45:67:89:AA");
            s.SetMac(TvB, "01:23:45:67:89:BB");
            s.SetCookie(TvA, "auth=a", DateTime.UtcNow.AddDays(5));
            s.SetCookie(TvB, "auth=b", DateTime.UtcNow.AddDays(9));
            s.ClearCookie(TvB);

            Assert.Equal("01:23:45:67:89:AA", s.GetMac(TvA));
            Assert.Equal("01:23:45:67:89:BB", s.GetMac(TvB));
            Assert.Equal("auth=a", s.GetCookie(TvA));
            Assert.Null(s.GetCookie(TvB));
        }

        [Fact]
        public void Ip_keys_ignore_spaces_and_case()
        {
            var s = Settings.Load(FilePath);
            s.SetMac(" My-TV.local ", "01:23:45:67:89:AB");
            Assert.Equal("01:23:45:67:89:AB", s.GetMac("my-tv.local"));
        }

        [Fact]
        public void Reading_an_unknown_ip_does_not_create_an_entry()
        {
            var s = Settings.Load(FilePath);
            s.GetMac(TvB);
            s.GetCookie(TvB);
            Assert.False(s.Tvs.ContainsKey(TvB));
        }

        [Fact]
        public void Migrates_the_1_1_0_format_to_the_saved_ip()
        {
            if (!OperatingSystem.IsWindows()) return;   // the 1.1.0 cookie is DPAPI-protected
            var exp = new DateTime(2026, 11, 11, 4, 20, 48, DateTimeKind.Utc);
            var protector = new Settings();
            protector.SetCookie("x", "auth=legacy", exp);
            var protectedCookie = protector.Entry("x").AuthCookieProtected;

            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath,
                "{\n  \"Language\": \"pt-BR\",\n  \"Ip\": \"192.168.1.50\",\n" +
                "  \"ClientId\": \"6f1c2a1e-0000-4000-8000-000000000001\",\n" +
                "  \"Mac\": \"01:23:45:67:89:AB\",\n" +
                "  \"AuthCookieProtected\": \"" + protectedCookie + "\",\n" +
                "  \"AuthExpiresUtc\": \"2026-11-11T04:20:48Z\"\n}");

            var s = Settings.Load(FilePath);
            Assert.Equal("01:23:45:67:89:AB", s.GetMac("192.168.1.50"));
            Assert.Equal("auth=legacy", s.GetCookie("192.168.1.50"));
            Assert.Equal(exp, s.GetAuthExpiresUtc("192.168.1.50"));
            Assert.Null(s.GetMac(TvA));
            Assert.Null(s.GetCookie(TvA));
            Assert.Equal("pt-BR", s.Language);
            Assert.Equal("6f1c2a1e-0000-4000-8000-000000000001", s.ClientId);

            // Written back in the new format, without the old top-level fields.
            using (var doc = JsonDocument.Parse(File.ReadAllText(FilePath)))
            {
                var root = doc.RootElement;
                Assert.False(root.TryGetProperty("Mac", out _));
                Assert.False(root.TryGetProperty("AuthCookieProtected", out _));
                Assert.False(root.TryGetProperty("AuthExpiresUtc", out _));
                Assert.True(root.GetProperty("Tvs").TryGetProperty("192.168.1.50", out _));
            }
            var again = Settings.Load(FilePath);
            Assert.Equal("auth=legacy", again.GetCookie("192.168.1.50"));
            Assert.Equal("01:23:45:67:89:AB", again.GetMac("192.168.1.50"));
        }

        [Fact]
        public void Migration_without_a_saved_ip_uses_the_default_ip()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath, "{ \"Mac\": \"01:23:45:67:89:AB\" }");
            var s = Settings.Load(FilePath);
            Assert.Equal("01:23:45:67:89:AB", s.GetMac(Settings.DefaultIp));
        }

        [Fact]
        public void Migration_moves_mac_cookie_and_expiry_on_any_os()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath,
                "{ \"Ip\": \"192.168.1.50\", \"Mac\": \"01:23:45:67:89:AB\", " +
                "\"AuthCookieProtected\": \"cHJvdGVjdGVk\", \"AuthExpiresUtc\": \"2026-11-11T04:20:48Z\" }");
            var s = Settings.Load(FilePath);
            Assert.Equal("01:23:45:67:89:AB", s.GetMac("192.168.1.50"));
            Assert.Equal("cHJvdGVjdGVk", s.Entry("192.168.1.50").AuthCookieProtected);
            Assert.Equal(new DateTime(2026, 11, 11, 4, 20, 48, DateTimeKind.Utc), s.GetAuthExpiresUtc("192.168.1.50"));
            Assert.Null(s.GetMac(TvA));
        }

        [Fact]
        public void Migration_does_not_invent_an_expiry()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath, "{ \"Ip\": \"192.168.1.50\", \"AuthCookieProtected\": \"cHJvdGVjdGVk\" }");
            var s = Settings.Load(FilePath);
            Assert.Equal("cHJvdGVjdGVk", s.Entry("192.168.1.50").AuthCookieProtected);
            Assert.Null(s.GetAuthExpiresUtc("192.168.1.50"));
        }

        [Fact]
        public void Existing_per_tv_entry_wins_over_legacy_fields()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath,
                "{ \"Ip\": \"192.168.1.50\", \"Mac\": \"01:23:45:67:89:00\", \"AuthCookieProtected\": \"b2xk\", " +
                "\"Tvs\": { \"192.168.1.50\": { \"Mac\": \"01:23:45:67:89:AB\", \"AuthCookieProtected\": \"bmV3\", " +
                "\"AuthExpiresUtc\": \"2026-11-11T04:20:48Z\" } } }");
            var s = Settings.Load(FilePath);
            Assert.Equal("01:23:45:67:89:AB", s.GetMac("192.168.1.50"));
            Assert.Equal("bmV3", s.Entry("192.168.1.50").AuthCookieProtected);
        }

        [Fact]
        public void New_format_file_is_not_rewritten_on_load()
        {
            var s = Settings.Load(FilePath);
            s.SetMac(TvA, "01:23:45:67:89:AB");
            s.Save(FilePath);
            var before = File.ReadAllText(FilePath);
            var stamp = File.GetLastWriteTimeUtc(FilePath);

            Settings.Load(FilePath);
            Assert.Equal(before, File.ReadAllText(FilePath));
            Assert.Equal(stamp, File.GetLastWriteTimeUtc(FilePath));
        }

        [Fact]
        public void Null_tv_entries_are_ignored_instead_of_losing_the_file()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath,
                "{ \"Ip\": \"192.168.1.50\", \"Tvs\": { \"192.168.1.50\": null, " +
                "\"192.168.1.51\": { \"Mac\": \"01:23:45:67:89:AB\" } } }");
            var s = Settings.Load(FilePath);
            Assert.Equal("192.168.1.50", s.Ip);
            Assert.Equal("01:23:45:67:89:AB", s.GetMac("192.168.1.51"));
            s.SetMac("192.168.1.50", "01:23:45:67:89:CD");
            Assert.Equal("01:23:45:67:89:CD", s.GetMac("192.168.1.50"));
        }

        [Fact]
        public void Keys_from_the_file_are_normalized()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath, "{ \"Tvs\": { \" My-TV.local \": { \"Mac\": \"01:23:45:67:89:AB\" } } }");
            var s = Settings.Load(FilePath);
            Assert.Equal("01:23:45:67:89:AB", s.GetMac("my-tv.local"));
            s.SetMac("MY-TV.LOCAL", "01:23:45:67:89:CD");
            Assert.Single(s.Tvs);
        }

        [Fact]
        public void Unreadable_file_is_kept_as_a_bad_copy_before_defaults_are_used()
        {
            Directory.CreateDirectory(_dir);
            File.WriteAllText(FilePath, "{ not json");
            Settings.Load(FilePath);
            Assert.Equal("{ not json", File.ReadAllText(FilePath + ".bad"));
        }
    }

    public class PowerOnSequenceTests
    {
        private readonly List<string> _log = new List<string>();
        private TimeSpan _elapsed;

        private Task Delay(TimeSpan t) { _elapsed += t; return Task.CompletedTask; }

        private Task<bool> Run(Func<int, bool> activeAtPoll, bool paired = true, bool throwing = false, int pollSeconds = 0)
        {
            int poll = 0;
            return PowerOnSequence.RunAsync(
                () => { _log.Add("wol"); return Task.CompletedTask; },
                () =>
                {
                    poll++;
                    _log.Add("poll@" + _elapsed.TotalSeconds);
                    _elapsed += TimeSpan.FromSeconds(pollSeconds);
                    if (throwing && poll == 1) throw new Exception("no reply");
                    return Task.FromResult(activeAtPoll(poll));
                },
                paired ? () => { _log.Add("setPower"); if (throwing) throw new Exception("error 15"); return Task.CompletedTask; } : (Func<Task>)null,
                () => { _log.Add("ircc"); return Task.CompletedTask; },
                Delay,
                () => _elapsed);
        }

        [Fact]
        public async Task Slow_polls_still_stop_near_twenty_seconds()
        {
            Assert.False(await Run(p => false, pollSeconds: 5));
            Assert.Equal(new[] { "wol", "poll@2", "setPower", "ircc", "poll@9", "poll@16" }, _log);
            Assert.True(_elapsed <= TimeSpan.FromSeconds(22), _elapsed.ToString());
        }

        [Fact]
        public async Task Stops_as_soon_as_wol_works()
        {
            Assert.True(await Run(p => p == 1));
            Assert.Equal(new[] { "wol", "poll@2" }, _log);
        }

        [Fact]
        public async Task Never_active_tries_fallbacks_after_six_seconds_then_polls_until_twenty()
        {
            Assert.False(await Run(p => false));
            Assert.Equal(new[] { "wol", "poll@2", "poll@4", "poll@6", "setPower", "ircc",
                                 "poll@8", "poll@10", "poll@12", "poll@14", "poll@16", "poll@18", "poll@20" }, _log);
            Assert.Equal(TimeSpan.FromSeconds(20), _elapsed);
        }

        [Fact]
        public async Task Active_after_fallbacks_stops_polling()
        {
            Assert.True(await Run(p => p == 5));
            Assert.Equal(new[] { "wol", "poll@2", "poll@4", "poll@6", "setPower", "ircc", "poll@8", "poll@10" }, _log);
        }

        [Fact]
        public async Task Not_paired_skips_set_power_status()
        {
            await Run(p => false, paired: false);
            Assert.DoesNotContain("setPower", _log);
            Assert.Contains("ircc", _log);
        }

        [Fact]
        public async Task Errors_are_swallowed()
        {
            Assert.False(await Run(p => false, throwing: true));
            Assert.Contains("ircc", _log);
            Assert.Equal("poll@20", _log[_log.Count - 1]);
        }
    }
}
