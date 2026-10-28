using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SonyTvRemote;
using Xunit;

namespace SonyTvRemote.Tests
{
    public class SettingsTests : IDisposable
    {
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
            Assert.Null(s.GetCookie());
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
            s.Mac = "01:23:45:67:89:AB";
            var id = s.EnsureClientId();
            var exp = new DateTime(2026, 11, 11, 4, 0, 0, DateTimeKind.Utc);
            s.SetCookie("auth=secretvalue", exp);
            s.Save(FilePath);

            Assert.DoesNotContain("secretvalue", File.ReadAllText(FilePath));

            var r = Settings.Load(FilePath);
            Assert.Equal("192.168.1.50", r.Ip);
            Assert.Equal("pt-BR", r.Language);
            Assert.Equal("01:23:45:67:89:AB", r.Mac);
            Assert.Equal(id, r.EnsureClientId());
            Assert.Equal("auth=secretvalue", r.GetCookie());
            Assert.Equal(exp, r.AuthExpiresUtc);
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
            s.SetCookie("auth=x", DateTime.UtcNow.AddDays(5));
            s.ClearCookie();
            Assert.Null(s.GetCookie());
            Assert.Null(s.AuthExpiresUtc);
        }

        [Fact]
        public void Unreadable_protected_cookie_is_treated_as_missing()
        {
            var s = Settings.Load(FilePath);
            s.AuthCookieProtected = "bm90IGRwYXBp";
            Assert.Null(s.GetCookie());
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
