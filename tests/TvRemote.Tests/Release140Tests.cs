using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SonyTvRemote;
using Xunit;

// 1.4.0: finding the TV when its IP changes. Fake data only (documentation and private test
// addresses, MACs like AA:BB:CC:DD:EE:01); nothing here touches the network.

namespace SonyTvRemote.Tests
{
    public class MacTests
    {
        [Fact]
        public void Normalize_uppercases_and_uses_colons()
        {
            Assert.Equal("AA:BB:CC:DD:EE:01", Mac.Normalize("aa-bb-cc-dd-ee-01"));
            Assert.Equal("AA:BB:CC:DD:EE:01", Mac.Normalize(" aa:bb:cc:dd:ee:01 "));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a mac")]
        [InlineData("AA:BB:CC:DD:EE")]
        [InlineData("AA:BB:CC:DD:EE:01:02")]
        [InlineData("AA:BB:CC:DD:EE:GG")]
        [InlineData("AABBCCDDEE01")]
        public void Normalize_gives_null_for_an_invalid_mac(string text) => Assert.Null(Mac.Normalize(text));
    }

    public class SsdpTests
    {
        [Fact]
        public void Search_text_has_the_exact_lines_CRLF_and_a_blank_line_at_the_end()
        {
            Assert.Equal(
                "M-SEARCH * HTTP/1.1\r\n" +
                "HOST: 239.255.255.250:1900\r\n" +
                "MAN: \"ssdp:discover\"\r\n" +
                "MX: 1\r\n" +
                "ST: urn:schemas-sony-com:service:IRCC:1\r\n" +
                "\r\n",
                Ssdp.BuildSearch("urn:schemas-sony-com:service:IRCC:1"));
        }

        [Fact]
        public void Two_search_targets_are_used()
        {
            Assert.Equal(new[]
            {
                "urn:schemas-sony-com:service:ScalarWebAPI:1",
                "urn:schemas-sony-com:service:IRCC:1"
            }, Ssdp.SearchTargets);
        }

        [Fact]
        public void Response_header_names_are_case_insensitive()
        {
            var text = "HTTP/1.1 200 OK\r\nCache-Control: max-age=1800\r\nlocation: http://10.0.0.20:52323/dmr.xml\r\nST: x\r\n\r\n";
            var c = Ssdp.ParseResponse(text, "10.0.0.20");
            Assert.Equal("10.0.0.20", c.Ip);
            Assert.Equal("http://10.0.0.20:52323/dmr.xml", c.Location);

            var upper = "HTTP/1.1 200 OK\r\nLOCATION: http://10.0.0.20:8080/desc.xml\r\n\r\n";
            Assert.Equal("http://10.0.0.20:8080/desc.xml", Ssdp.ParseResponse(upper, "10.0.0.20").Location);
        }

        [Theory]
        [InlineData("http://10.0.0.20:52323/dmr.xml", "http://10.0.0.20:52323/dmr.xml")]
        [InlineData("HTTP://10.0.0.20:52323/dmr.xml", "HTTP://10.0.0.20:52323/dmr.xml")]
        [InlineData("http://10.0.0.99:52323/dmr.xml", "http://10.0.0.20:52323/dmr.xml")]   // another host
        [InlineData("http://evil.example/dmr.xml", "http://10.0.0.20:52323/dmr.xml")]
        [InlineData("https://10.0.0.20/dmr.xml", "http://10.0.0.20:52323/dmr.xml")]        // not http://
        [InlineData("ftp://10.0.0.20/dmr.xml", "http://10.0.0.20:52323/dmr.xml")]
        [InlineData("not a url", "http://10.0.0.20:52323/dmr.xml")]
        [InlineData("", "http://10.0.0.20:52323/dmr.xml")]
        [InlineData(null, "http://10.0.0.20:52323/dmr.xml")]
        public void Location_is_used_only_when_http_and_the_host_is_the_sender(string location, string expected)
            => Assert.Equal(expected, Ssdp.ResolveLocation(location, "10.0.0.20"));

        [Fact]
        public void Response_without_a_location_header_uses_the_default_address()
        {
            var c = Ssdp.ParseResponse("HTTP/1.1 200 OK\r\nST: x\r\n\r\n", "10.0.0.20");
            Assert.Equal("http://10.0.0.20:52323/dmr.xml", c.Location);
        }
    }

    public class DmrParserTests
    {
        private const string Valid =
            "<?xml version=\"1.0\"?>" +
            "<root xmlns=\"urn:schemas-upnp-org:device-1-0\" xmlns:av=\"urn:schemas-sony-com:av\">" +
            "<device>" +
            "<deviceType>urn:schemas-upnp-org:device:MediaRenderer:1</deviceType>" +
            "<friendlyName>Test TV</friendlyName>" +
            "<manufacturer>Sony Corporation</manufacturer>" +
            "<modelName>KDL-TEST</modelName>" +
            "<UDN>uuid:00000000-0000-0000-0000-000000000000</UDN>" +
            "<serviceList><service><serviceType>urn:schemas-sony-com:service:IRCC:1</serviceType></service></serviceList>" +
            "<av:X_ScalarWebAPI_DeviceInfo><av:X_ScalarWebAPI_BaseURL>http://10.0.0.20/sony</av:X_ScalarWebAPI_BaseURL></av:X_ScalarWebAPI_DeviceInfo>" +
            "</device></root>";

        private static string Device(string maker, string extra, string names = "<friendlyName>Test TV</friendlyName><modelName>KDL-TEST</modelName>")
            => "<root xmlns=\"urn:schemas-upnp-org:device-1-0\"><device>" + names +
               "<manufacturer>" + maker + "</manufacturer>" + extra + "</device></root>";

        private const string IrccOnly = "<serviceList><service><serviceType>urn:schemas-sony-com:service:IRCC:1</serviceType></service></serviceList>";

        [Fact]
        public void A_valid_sony_tv_is_accepted()
        {
            var tv = DmrParser.Parse(Valid, "10.0.0.20");
            Assert.Equal("10.0.0.20", tv.Ip);
            Assert.Equal("Test TV", tv.Name);
            Assert.Equal("KDL-TEST", tv.Model);
        }

        [Fact]
        public void The_manufacturer_is_compared_ignoring_case()
            => Assert.NotNull(DmrParser.Parse(Device("SONY corp", IrccOnly), "10.0.0.20"));

        [Fact]
        public void Another_brand_is_refused()
            => Assert.Null(DmrParser.Parse(Device("Other Corp", IrccOnly), "10.0.0.20"));

        [Fact]
        public void Sony_without_scalar_and_without_ircc_is_refused()
            => Assert.Null(DmrParser.Parse(Device("Sony Corporation", "<serviceList/>"), "10.0.0.20"));

        [Fact]
        public void Sony_with_only_the_scalar_url_is_accepted()
        {
            var xml = Device("Sony Corporation",
                "<X_ScalarWebAPI_BaseURL xmlns=\"urn:schemas-sony-com:av\">http://10.0.0.20/sony</X_ScalarWebAPI_BaseURL>");
            Assert.NotNull(DmrParser.Parse(xml, "10.0.0.20"));
        }

        [Fact]
        public void A_doctype_is_refused_and_does_not_hang()
        {
            var xml = "<?xml version=\"1.0\"?><!DOCTYPE root [<!ENTITY a \"aaaaaaaaaa\"><!ENTITY b \"&a;&a;&a;&a;&a;\">]>" +
                      Device("Sony Corporation", IrccOnly).Replace("<modelName>", "<modelName>&b;");
            Assert.Null(DmrParser.Parse(xml, "10.0.0.20"));
        }

        [Fact]
        public void Broken_xml_gives_null()
        {
            Assert.Null(DmrParser.Parse("<root><device><manufacturer>Sony", "10.0.0.20"));
            Assert.Null(DmrParser.Parse("", "10.0.0.20"));
            Assert.Null(DmrParser.Parse(null, "10.0.0.20"));
        }

        [Fact]
        public void The_name_falls_back_to_the_model_and_then_to_the_ip()
        {
            Assert.Equal("KDL-TEST", DmrParser.Parse(Device("Sony", IrccOnly, "<modelName>KDL-TEST</modelName>"), "10.0.0.20").Name);
            Assert.Equal("10.0.0.20", DmrParser.Parse(Device("Sony", IrccOnly, ""), "10.0.0.20").Name);
            Assert.Equal("KDL-TEST", DmrParser.Parse(Device("Sony", IrccOnly, "<friendlyName> </friendlyName><modelName>KDL-TEST</modelName>"), "10.0.0.20").Name);
        }
    }

    public class ScanRangeTests
    {
        private static IPAddress Ip(string s) => IPAddress.Parse(s);

        [Fact]
        public void A_slash_24_gives_253_addresses_without_the_pc()
        {
            var list = ScanRange.Addresses(Ip("10.0.0.5"), 24);
            Assert.Equal(253, list.Count);
            Assert.DoesNotContain(Ip("10.0.0.5"), list);
            Assert.DoesNotContain(Ip("10.0.0.0"), list);      // network address
            Assert.DoesNotContain(Ip("10.0.0.255"), list);    // broadcast address
            Assert.Contains(Ip("10.0.0.1"), list);
            Assert.Contains(Ip("10.0.0.254"), list);
        }

        [Fact]
        public void A_slash_16_scans_only_the_slash_24_of_the_pc()
        {
            var list = ScanRange.Addresses(Ip("10.0.7.9"), 16);
            Assert.Equal(253, list.Count);
            Assert.All(list, a => Assert.StartsWith("10.0.7.", a.ToString()));
        }

        [Fact]
        public void A_slash_26_gives_61_addresses()
        {
            var list = ScanRange.Addresses(Ip("10.0.0.70"), 26);   // subnet 10.0.0.64 - 10.0.0.127
            Assert.Equal(61, list.Count);
            Assert.All(list, a =>
            {
                var last = int.Parse(a.ToString().Split('.')[3]);
                Assert.InRange(last, 65, 126);
            });
        }

        [Theory]
        [InlineData(31)]
        [InlineData(32)]
        public void A_slash_31_or_32_gives_nothing(int prefix)
            => Assert.Empty(ScanRange.Addresses(Ip("10.0.0.5"), prefix));

        [Theory]
        [InlineData("198.51.100.5")]
        [InlineData("8.8.8.8")]
        [InlineData("169.254.10.20")]
        [InlineData("172.32.0.1")]
        public void A_public_or_link_local_address_gives_nothing(string pc)
            => Assert.Empty(ScanRange.Addresses(Ip(pc), 24));

        [Theory]
        [InlineData("10.1.2.3", true)]
        [InlineData("172.16.0.1", true)]
        [InlineData("172.31.255.1", true)]
        [InlineData("172.15.0.1", false)]
        [InlineData("192.168.0.1", true)]
        [InlineData("192.169.0.1", false)]
        [InlineData("169.254.0.1", false)]
        public void Only_the_three_private_ranges_count(string ip, bool expected)
            => Assert.Equal(expected, ScanRange.IsPrivate(Ip(ip)));

        [Fact]
        public void Two_adapters_on_the_same_slash_24_count_once()
        {
            var one = ScanRange.ForInterfaces(new[] { new KeyValuePair<IPAddress, int>(Ip("10.0.0.5"), 24) });
            var two = ScanRange.ForInterfaces(new[]
            {
                new KeyValuePair<IPAddress, int>(Ip("10.0.0.5"), 24),
                new KeyValuePair<IPAddress, int>(Ip("10.0.0.6"), 24)
            });
            Assert.Equal(253, one.Count);
            Assert.Equal(252, two.Count);                     // the PC's second address is skipped too
            Assert.Equal(two.Count, two.Select(a => a.ToString()).Distinct().Count());
            Assert.DoesNotContain(Ip("10.0.0.6"), two);
        }

        [Fact]
        public void Adapters_on_different_subnets_are_both_scanned()
        {
            var list = ScanRange.ForInterfaces(new[]
            {
                new KeyValuePair<IPAddress, int>(Ip("10.0.0.5"), 24),
                new KeyValuePair<IPAddress, int>(Ip("10.0.1.5"), 24)
            });
            Assert.Equal(506, list.Count);
        }
    }

    public class FinderTests
    {
        private class Fake
        {
            public List<Candidate> Ssdp = new List<Candidate>();
            public List<Candidate> Scan = new List<Candidate>();
            public Dictionary<string, FoundTv> Tvs = new Dictionary<string, FoundTv>();
            public Dictionary<string, string> Macs = new Dictionary<string, string>();
            public int ScanCalls;
            public HashSet<string> MacDelayed = new HashSet<string>();
            public bool SlowReadCancelled;
            public Func<CancellationToken, Task> OnSsdp;

            public DiscoveryParts Parts() => new DiscoveryParts
            {
                Ssdp = async ct =>
                {
                    if (OnSsdp != null) await OnSsdp(ct);
                    return Ssdp;
                },
                Scan = ct => { ScanCalls++; return Task.FromResult(Scan); },
                Describe = (c, ct) => Task.FromResult(Tvs.TryGetValue(c.Ip, out var tv) ? new FoundTv { Ip = tv.Ip, Name = tv.Name, Model = tv.Model } : null),
                ReadMac = async (ip, ct) =>
                {
                    if (MacDelayed.Contains(ip))
                    {
                        try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
                        catch (OperationCanceledException) { SlowReadCancelled = true; throw; }
                    }
                    return Macs.TryGetValue(ip, out var m) ? m : null;
                }
            };

            public Fake Tv(string ip, string mac, bool viaSsdp)
            {
                var c = new Candidate { Ip = ip, Location = "http://" + ip + ":52323/dmr.xml" };
                (viaSsdp ? Ssdp : Scan).Add(c);
                Tvs[ip] = new FoundTv { Ip = ip, Name = "TV " + ip, Model = "M" };
                Macs[ip] = mac;
                return this;
            }
        }

        private static readonly TimeSpan Soon = TimeSpan.FromSeconds(5);

        [Fact]
        public async Task FindAll_does_not_scan_when_ssdp_found_a_valid_tv()
        {
            var f = new Fake().Tv("10.0.0.20", "aa-bb-cc-dd-ee-01", viaSsdp: true).Tv("10.0.0.30", "AA:BB:CC:DD:EE:02", viaSsdp: false);
            var tvs = await Finder.FindAll(f.Parts(), CancellationToken.None);
            Assert.Equal(0, f.ScanCalls);
            var tv = Assert.Single(tvs);
            Assert.Equal("10.0.0.20", tv.Ip);
            Assert.Equal("AA:BB:CC:DD:EE:01", tv.Mac);   // read, then normalized
        }

        [Fact]
        public async Task FindAll_scans_when_ssdp_found_no_valid_tv()
        {
            var f = new Fake().Tv("10.0.0.30", "AA:BB:CC:DD:EE:02", viaSsdp: false);
            f.Ssdp.Add(new Candidate { Ip = "10.0.0.99", Location = "http://10.0.0.99:52323/dmr.xml" });   // not a Sony TV
            var tvs = await Finder.FindAll(f.Parts(), CancellationToken.None);
            Assert.Equal(1, f.ScanCalls);
            Assert.Equal("10.0.0.30", Assert.Single(tvs).Ip);
        }

        [Fact]
        public async Task FindAll_lists_each_ip_once()
        {
            var f = new Fake().Tv("10.0.0.20", "AA:BB:CC:DD:EE:01", viaSsdp: true);
            f.Ssdp.Add(new Candidate { Ip = "10.0.0.20", Location = "http://10.0.0.20:52323/dmr.xml" });
            Assert.Single(await Finder.FindAll(f.Parts(), CancellationToken.None));
        }

        [Fact]
        public async Task FindAll_returns_empty_when_nothing_answers()
        {
            var f = new Fake();
            Assert.Empty(await Finder.FindAll(f.Parts(), CancellationToken.None));
            Assert.Equal(1, f.ScanCalls);
        }

        [Fact]
        public async Task FindByMac_stops_at_the_first_matching_mac()
        {
            var f = new Fake().Tv("10.0.0.20", "aa:bb:cc:dd:ee:01", viaSsdp: true).Tv("10.0.0.21", "AA:BB:CC:DD:EE:02", viaSsdp: true);
            f.Macs["10.0.0.21"] = "AA:BB:CC:DD:EE:02";
            f.MacDelayed.Add("10.0.0.21");
            var ip = await Finder.FindByMac(f.Parts(), "AA-BB-CC-DD-EE-01", CancellationToken.None).WaitAsync(Soon);
            Assert.Equal("10.0.0.20", ip);
            Assert.Equal(0, f.ScanCalls);
            Assert.True(f.SlowReadCancelled);
        }

        [Fact]
        public async Task FindByMac_scans_when_ssdp_found_only_other_macs()
        {
            var f = new Fake().Tv("10.0.0.20", "AA:BB:CC:DD:EE:02", viaSsdp: true).Tv("10.0.0.30", "AA:BB:CC:DD:EE:01", viaSsdp: false);
            var ip = await Finder.FindByMac(f.Parts(), "AA:BB:CC:DD:EE:01", CancellationToken.None);
            Assert.Equal("10.0.0.30", ip);
            Assert.Equal(1, f.ScanCalls);
        }

        [Fact]
        public async Task FindByMac_gives_null_when_the_mac_is_nowhere()
        {
            var f = new Fake().Tv("10.0.0.20", "AA:BB:CC:DD:EE:02", viaSsdp: true);
            Assert.Null(await Finder.FindByMac(f.Parts(), "AA:BB:CC:DD:EE:01", CancellationToken.None));
            Assert.Equal(1, f.ScanCalls);
        }

        [Fact]
        public async Task FindByMac_with_an_invalid_mac_gives_null_without_searching()
        {
            var f = new Fake().Tv("10.0.0.20", "AA:BB:CC:DD:EE:01", viaSsdp: true);
            Assert.Null(await Finder.FindByMac(f.Parts(), "junk", CancellationToken.None));
            Assert.Equal(0, f.ScanCalls);
        }

        [Fact]
        public async Task Cancelling_stops_both_searches()
        {
            var f = new Fake();
            f.OnSsdp = ct => Task.Delay(Timeout.Infinite, ct);
            using (var cts = new CancellationTokenSource())
            {
                var all = Finder.FindAll(f.Parts(), cts.Token);
                var byMac = Finder.FindByMac(f.Parts(), "AA:BB:CC:DD:EE:01", cts.Token);
                cts.CancelAfter(50);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => all.WaitAsync(Soon));
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => byMac.WaitAsync(Soon));
            }
            Assert.Equal(0, f.ScanCalls);
        }

        [Fact]
        public async Task The_time_limit_ends_the_search_without_an_error()
        {
            var f = new Fake();
            f.OnSsdp = ct => Task.Delay(Timeout.Infinite, ct);
            var limit = TimeSpan.FromMilliseconds(100);
            Assert.Empty(await Finder.FindAll(f.Parts(), CancellationToken.None, limit).WaitAsync(Soon));
            Assert.Null(await Finder.FindByMac(f.Parts(), "AA:BB:CC:DD:EE:01", CancellationToken.None, limit).WaitAsync(Soon));
        }

        [Fact]
        public async Task A_failing_part_does_not_stop_the_search()
        {
            var f = new Fake().Tv("10.0.0.30", "AA:BB:CC:DD:EE:01", viaSsdp: false);
            f.OnSsdp = ct => throw new InvalidOperationException("no multicast");
            Assert.Equal("10.0.0.30", await Finder.FindByMac(f.Parts(), "AA:BB:CC:DD:EE:01", CancellationToken.None));
        }
    }

    public class SettingsMoveTests
    {
        private static readonly DateTime Soon = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Later = new DateTime(2030, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        private static TvData Tv(string mac, string cookie = null, DateTime? expires = null)
            => new TvData { Mac = mac, AuthCookieProtected = cookie, AuthExpiresUtc = expires };

        private static Settings With(params (string ip, TvData tv)[] entries)
        {
            var s = new Settings();
            foreach (var e in entries) s.Tvs[e.ip] = e.tv;
            return s;
        }

        [Fact]
        public void FindIpByMac_compares_after_normalizing()
        {
            var s = With(("10.0.0.20", Tv("aa-bb-cc-dd-ee-01")), ("10.0.0.21", Tv("AA:BB:CC:DD:EE:02")));
            Assert.Equal("10.0.0.20", s.FindIpByMac("AA:BB:CC:DD:EE:01"));
            Assert.Equal("10.0.0.21", s.FindIpByMac("aa-bb-cc-dd-ee-02"));
            Assert.Null(s.FindIpByMac("AA:BB:CC:DD:EE:03"));
            Assert.Null(s.FindIpByMac("junk"));
            Assert.Null(s.FindIpByMac(null));
        }

        [Fact]
        public void FindIpByMac_can_leave_one_ip_out()
        {
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01")));
            Assert.Null(s.FindIpByMac("AA:BB:CC:DD:EE:01", "10.0.0.20"));
            Assert.Equal("10.0.0.20", s.FindIpByMac("AA:BB:CC:DD:EE:01", "10.0.0.99"));
        }

        [Fact]
        public void MoveTv_with_the_same_ip_does_nothing()
        {
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "c1", Soon)));
            s.Ip = "10.0.0.20";
            Assert.False(s.MoveTv("10.0.0.20", " 10.0.0.20 "));
            Assert.Single(s.Tvs);
            Assert.Equal("c1", s.Tvs["10.0.0.20"].AuthCookieProtected);
        }

        [Fact]
        public void MoveTv_without_data_at_fromIp_does_nothing()
        {
            var s = With(("10.0.0.30", Tv("AA:BB:CC:DD:EE:09", "other", Soon)));
            s.Ip = "10.0.0.30";
            Assert.False(s.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.Equal("other", s.Tvs["10.0.0.30"].AuthCookieProtected);
            Assert.Equal("10.0.0.30", s.Ip);
        }

        [Fact]
        public void MoveTv_to_an_empty_ip_moves_everything_and_updates_the_ip()
        {
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "c1", Soon)));
            s.Ip = "10.0.0.20";
            Assert.True(s.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.False(s.Tvs.ContainsKey("10.0.0.20"));       // another device may get the old IP
            var moved = s.Tvs["10.0.0.30"];
            Assert.Equal("AA:BB:CC:DD:EE:01", moved.Mac);
            Assert.Equal("c1", moved.AuthCookieProtected);
            Assert.Equal(Soon, moved.AuthExpiresUtc);
            Assert.Equal("10.0.0.30", s.Ip);
        }

        [Fact]
        public void MoveTv_with_the_same_mac_keeps_the_pairing_that_expires_later_cookie_and_expiry_together()
        {
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "old-cookie", Soon)),
                         ("10.0.0.30", Tv("aa-bb-cc-dd-ee-01", "new-cookie", Later)));
            Assert.True(s.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.Equal("new-cookie", s.Tvs["10.0.0.30"].AuthCookieProtected);
            Assert.Equal(Later, s.Tvs["10.0.0.30"].AuthExpiresUtc);

            var t = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "later-cookie", Later)),
                         ("10.0.0.30", Tv("AA:BB:CC:DD:EE:01", "soon-cookie", Soon)));
            Assert.True(t.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.Equal("later-cookie", t.Tvs["10.0.0.30"].AuthCookieProtected);
            Assert.Equal(Later, t.Tvs["10.0.0.30"].AuthExpiresUtc);
            Assert.False(t.Tvs.ContainsKey("10.0.0.20"));
        }

        [Fact]
        public void MoveTv_with_the_same_mac_never_mixes_a_cookie_with_the_other_expiry()
        {
            // The old IP has a cookie that expires later but the new IP has none: both fields come from the old one.
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "old-cookie", Later)),
                         ("10.0.0.30", Tv("AA:BB:CC:DD:EE:01")));
            Assert.True(s.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.Equal("old-cookie", s.Tvs["10.0.0.30"].AuthCookieProtected);
            Assert.Equal(Later, s.Tvs["10.0.0.30"].AuthExpiresUtc);
        }

        [Fact]
        public void MoveTv_replaces_the_data_at_the_new_ip_when_the_mac_differs()
        {
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "c1", Soon)),
                         ("10.0.0.30", Tv("AA:BB:CC:DD:EE:99", "other", Later)));
            Assert.True(s.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.Equal("AA:BB:CC:DD:EE:01", s.Tvs["10.0.0.30"].Mac);
            Assert.Equal("c1", s.Tvs["10.0.0.30"].AuthCookieProtected);
            Assert.Equal(Soon, s.Tvs["10.0.0.30"].AuthExpiresUtc);
            Assert.False(s.Tvs.ContainsKey("10.0.0.20"));
        }

        [Fact]
        public void MoveTv_replaces_the_data_at_the_new_ip_when_it_has_no_mac()
        {
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "c1", Soon)),
                         ("10.0.0.30", Tv(null, "stray", Later)));
            Assert.True(s.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.Equal("c1", s.Tvs["10.0.0.30"].AuthCookieProtected);
            Assert.Equal(Soon, s.Tvs["10.0.0.30"].AuthExpiresUtc);
        }

        [Fact]
        public void MoveTv_keys_are_the_trimmed_lowercase_ip_like_everywhere_else()
        {
            var s = With(("10.0.0.20", Tv("AA:BB:CC:DD:EE:01", "c1", Soon)));
            Assert.True(s.MoveTv(" 10.0.0.20 ", " 10.0.0.30 "));
            Assert.True(s.Tvs.ContainsKey("10.0.0.30"));
            Assert.Equal("10.0.0.30", s.Ip);
            Assert.Equal("AA:BB:CC:DD:EE:01", s.GetMac("10.0.0.30"));
            Assert.Null(s.GetMac("10.0.0.20"));
        }

        [Fact]
        public void The_pairing_still_decrypts_at_the_new_ip()
        {
            if (!OperatingSystem.IsWindows()) return;
            var s = new Settings();
            s.SetMac("10.0.0.20", "AA:BB:CC:DD:EE:01");
            s.SetCookie("10.0.0.20", "auth=secret", DateTime.UtcNow.AddDays(10));
            Assert.True(s.MoveTv("10.0.0.20", "10.0.0.30"));
            Assert.Equal("auth=secret", s.GetCookie("10.0.0.30"));
            Assert.Null(s.GetCookie("10.0.0.20"));
        }
    }

    public class AutoSearchTests
    {
        [Fact]
        public void It_runs_only_for_a_saved_mac_without_another_search_or_the_dialog()
        {
            Assert.True(AutoSearch.CanStart(macSaved: true, searching: false, dialogOpen: false));
            Assert.False(AutoSearch.CanStart(macSaved: false, searching: false, dialogOpen: false));
            Assert.False(AutoSearch.CanStart(macSaved: true, searching: true, dialogOpen: false));
            Assert.False(AutoSearch.CanStart(macSaved: true, searching: false, dialogOpen: true));
        }

        [Fact]
        public void After_no_response_it_waits_30_seconds_between_searches()
        {
            Assert.True(AutoSearch.AfterNoResponse(true, false, false, null));
            Assert.False(AutoSearch.AfterNoResponse(true, false, false, TimeSpan.FromSeconds(29.9)));
            Assert.True(AutoSearch.AfterNoResponse(true, false, false, TimeSpan.FromSeconds(30)));
        }

        [Fact]
        public void After_no_response_it_also_skips_without_a_mac_while_searching_and_with_the_dialog_open()
        {
            Assert.False(AutoSearch.AfterNoResponse(false, false, false, null));
            Assert.False(AutoSearch.AfterNoResponse(true, true, false, null));
            Assert.False(AutoSearch.AfterNoResponse(true, false, true, null));
        }
    }

    public class Release140Tests
    {
        private static readonly string[] NewKeys =
        {
            "Find", "FindTip", "Pair", "PairTip", "NotPaired", "FindTitle", "FindSearching", "FindPick", "FindNone",
            "FindSaved", "FindCurrent", "FindAgain", "TvChosen", "TvMoved", "TvMovedRetry", "Searching",
            "SearchingAfterNoResponse", "PowerOnMoved"
        };

        [Fact]
        public void The_new_keys_exist_in_both_languages()
        {
            foreach (var lang in new[] { Lang.EnUs, Lang.PtBr })
                foreach (var key in NewKeys)
                {
                    Assert.True(Strings.Table(lang).ContainsKey(key), lang + ":" + key);
                    Assert.False(string.IsNullOrWhiteSpace(Strings.Get(lang, key)), lang + ":" + key);
                }
        }

        [Fact]
        public void The_buttons_and_notices_have_the_specified_texts()
        {
            Assert.Equal("Find TV", Strings.Get(Lang.EnUs, "Find"));
            Assert.Equal("Procurar", Strings.Get(Lang.PtBr, "Find"));
            Assert.Equal("Pair", Strings.Get(Lang.EnUs, "Pair"));
            Assert.Equal("Parear", Strings.Get(Lang.PtBr, "Pair"));
            Assert.Equal("Pair with TV", Strings.Get(Lang.EnUs, "PinTitle"));
            Assert.Equal("Parear com a TV", Strings.Get(Lang.PtBr, "PinTitle"));
            Assert.Equal("The TV changed IP: now 10.0.0.30. Click again.", Strings.Get(Lang.EnUs, "TvMovedRetry", "10.0.0.30"));
            Assert.Equal("A TV mudou de IP: agora 10.0.0.30. Clique de novo.", Strings.Get(Lang.PtBr, "TvMovedRetry", "10.0.0.30"));
            Assert.Equal("TV selected: Test TV (10.0.0.30).", Strings.Get(Lang.EnUs, "TvChosen", "Test TV", "10.0.0.30"));
            Assert.Equal("Ligada. A TV mudou de IP: agora 10.0.0.30.", Strings.Get(Lang.PtBr, "PowerOnMoved", "10.0.0.30"));
        }

        [Fact]
        public void The_version_is_1_5_0() => Assert.Equal("1.5.0", AppInfo.Version);
    }
}
