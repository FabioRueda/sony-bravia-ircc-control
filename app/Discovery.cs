using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

// Finding the TV on the network when its IP changes. The pure logic (MAC, SSDP text,
// device description, scan addresses, orchestration) is separate from the network
// parts, which are handed in as functions so the tests run without a network.

namespace SonyTvRemote
{
    public static class Mac
    {
        // "aa-bb-cc-dd-ee-01" -> "AA:BB:CC:DD:EE:01". Null when it is not 6 hex bytes.
        public static string Normalize(string mac)
        {
            if (string.IsNullOrWhiteSpace(mac)) return null;
            var parts = mac.Trim().Split(':', '-');
            if (parts.Length != 6) return null;
            foreach (var p in parts)
                if (p.Length != 2 || !Uri.IsHexDigit(p[0]) || !Uri.IsHexDigit(p[1])) return null;
            return string.Join(":", parts).ToUpperInvariant();
        }
    }

    // A TV found on the network.
    public class FoundTv
    {
        public string Ip;
        public string Name;
        public string Model;
        public string Mac;
    }

    // An address that may be a TV, with the URL of its device description.
    public class Candidate
    {
        public string Ip;
        public string Location;
    }

    public static class Ssdp
    {
        public const string Address = "239.255.255.250";
        public const int Port = 1900;
        public static readonly string[] SearchTargets =
        {
            "urn:schemas-sony-com:service:ScalarWebAPI:1",
            "urn:schemas-sony-com:service:IRCC:1"
        };

        public static string BuildSearch(string searchTarget)
            => "M-SEARCH * HTTP/1.1\r\n" +
               "HOST: " + Address + ":" + Port + "\r\n" +
               "MAN: \"ssdp:discover\"\r\n" +
               "MX: 1\r\n" +
               "ST: " + searchTarget + "\r\n" +
               "\r\n";

        // The device that answered and where to read its description. Header names are
        // case-insensitive. Null when there is no sender address.
        public static Candidate ParseResponse(string text, string senderIp)
        {
            if (string.IsNullOrWhiteSpace(senderIp)) return null;
            string location = null;
            var lines = (text ?? "").Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                if (line.Substring(0, colon).Trim().Equals("LOCATION", StringComparison.OrdinalIgnoreCase))
                {
                    location = line.Substring(colon + 1).Trim();
                    break;
                }
            }
            return new Candidate { Ip = senderIp, Location = ResolveLocation(location, senderIp) };
        }

        // The LOCATION is used only when it is http:// and points at the device that answered:
        // another device on the network must not make the app fetch from a different address.
        public static string ResolveLocation(string location, string senderIp)
        {
            var fallback = "http://" + senderIp + ":52323/dmr.xml";
            var loc = (location ?? "").Trim();
            if (!loc.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return fallback;
            if (!Uri.TryCreate(loc, UriKind.Absolute, out var uri)) return fallback;
            return uri.Host == senderIp ? loc : fallback;
        }
    }

    public static class DmrParser
    {
        private const string IrccService = "urn:schemas-sony-com:service:IRCC:1";

        // Reads dmr.xml. Null unless it is a Sony device with the Scalar Web API or the IRCC service,
        // or when the XML is broken or carries a DTD (the file comes from the network).
        public static FoundTv Parse(string xml, string ip)
        {
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                XDocument doc;
                using (var text = new StringReader(xml ?? ""))
                using (var reader = XmlReader.Create(text, settings))
                    doc = XDocument.Load(reader);

                string First(string name) => doc.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value?.Trim();

                var maker = First("manufacturer");
                if (maker == null || maker.IndexOf("Sony", StringComparison.OrdinalIgnoreCase) < 0) return null;

                bool scalar = doc.Descendants().Any(e => e.Name.LocalName == "X_ScalarWebAPI_BaseURL");
                bool ircc = doc.Descendants().Any(e => e.Name.LocalName == "serviceType" && e.Value.Trim() == IrccService);
                if (!scalar && !ircc) return null;

                var friendly = First("friendlyName");
                var model = First("modelName");
                var name = !string.IsNullOrEmpty(friendly) ? friendly : !string.IsNullOrEmpty(model) ? model : ip;
                return new FoundTv { Ip = ip, Name = name, Model = model ?? "" };
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    public static class ScanRange
    {
        // Only private IPv4 networks: 10/8, 172.16/12 and 192.168/16. Not 169.254.x.x, not public.
        public static bool IsPrivate(IPAddress ip)
        {
            if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            var b = ip.GetAddressBytes();
            return b[0] == 10
                || (b[0] == 172 && (b[1] & 0xF0) == 16)
                || (b[0] == 192 && b[1] == 168);
        }

        // The /24 around the PC (or the smaller subnet of the mask), without the network address,
        // the broadcast address and the PC itself. Nothing for /31, /32 or a non-private address.
        public static List<IPAddress> Addresses(IPAddress pc, int prefix)
        {
            var result = new List<IPAddress>();
            if (!IsPrivate(pc) || prefix >= 31) return result;
            int effective = Math.Max(prefix, 24);
            uint addr = ToUInt(pc);
            uint mask = uint.MaxValue << (32 - effective);
            uint network = addr & mask;
            uint broadcast = network | ~mask;
            for (uint a = network + 1; a < broadcast; a++)
                if (a != addr) result.Add(FromUInt(a));
            return result;
        }

        // Every adapter address together: a subnet shared by two adapters is scanned once,
        // and none of the PC's own addresses is scanned.
        public static List<IPAddress> ForInterfaces(IEnumerable<KeyValuePair<IPAddress, int>> local)
        {
            var list = local.ToList();
            var own = new HashSet<string>(list.Select(l => l.Key.ToString()));
            var seen = new HashSet<string>();
            var result = new List<IPAddress>();
            foreach (var l in list)
                foreach (var ip in Addresses(l.Key, l.Value))
                    if (!own.Contains(ip.ToString()) && seen.Add(ip.ToString())) result.Add(ip);
            return result;
        }

        private static uint ToUInt(IPAddress ip)
        {
            var b = ip.GetAddressBytes();
            return (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
        }

        private static IPAddress FromUInt(uint a)
            => new IPAddress(new[] { (byte)(a >> 24), (byte)(a >> 16), (byte)(a >> 8), (byte)a });
    }

    // The network parts, as functions.
    public class DiscoveryParts
    {
        public Func<CancellationToken, Task<List<Candidate>>> Ssdp;
        public Func<CancellationToken, Task<List<Candidate>>> Scan;
        // The TV described by the candidate, or null when it is not a Sony TV.
        public Func<Candidate, CancellationToken, Task<FoundTv>> Describe;
        public Func<string, CancellationToken, Task<string>> ReadMac;
    }

    public static class Finder
    {
        public static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

        // SSDP first; the scan only when SSDP found no valid Sony TV. Then the MAC of each TV.
        // Ends within the time limit (with what was found so far); throws when ct is cancelled.
        public static async Task<List<FoundTv>> FindAll(DiscoveryParts parts, CancellationToken ct, TimeSpan? limit = null)
        {
            var tvs = new List<FoundTv>();
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(limit ?? Limit);
                try
                {
                    tvs = await DescribeAll(parts, await Candidates(parts.Ssdp, cts.Token), cts.Token);
                    if (tvs.Count == 0)
                        tvs = await DescribeAll(parts, await Candidates(parts.Scan, cts.Token), cts.Token);
                    await ReadMacs(parts, tvs, cts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            }
            ct.ThrowIfCancellationRequested();
            return tvs;
        }

        // The IP of the TV with this MAC, or null. SSDP first; the scan only when no TV from SSDP
        // has that MAC. Stops as soon as it finds the MAC.
        public static async Task<string> FindByMac(DiscoveryParts parts, string mac, CancellationToken ct, TimeSpan? limit = null)
        {
            var want = Mac.Normalize(mac);
            if (want == null) return null;
            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                cts.CancelAfter(limit ?? Limit);
                try
                {
                    var hit = await Match(parts, await Candidates(parts.Ssdp, cts.Token), want, cts.Token);
                    if (hit == null) hit = await Match(parts, await Candidates(parts.Scan, cts.Token), want, cts.Token);
                    return hit;
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    return null;
                }
            }
        }

        // One failing part never stops the search; cancellation does.
        private static async Task<List<Candidate>> Candidates(Func<CancellationToken, Task<List<Candidate>>> source, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            List<Candidate> list = null;
            try { list = await source(ct); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { }
            ct.ThrowIfCancellationRequested();
            var seen = new HashSet<string>();
            return (list ?? new List<Candidate>()).Where(c => c != null && c.Ip != null && seen.Add(c.Ip)).ToList();
        }

        private static async Task<List<FoundTv>> DescribeAll(DiscoveryParts parts, List<Candidate> candidates, CancellationToken ct)
        {
            var results = await Task.WhenAll(candidates.Select(async c =>
            {
                try { return await parts.Describe(c, ct); }
                catch (Exception) { return null; }
            }));
            ct.ThrowIfCancellationRequested();
            return results.Where(t => t != null).ToList();
        }

        private static async Task ReadMacs(DiscoveryParts parts, List<FoundTv> tvs, CancellationToken ct)
        {
            await Task.WhenAll(tvs.Select(async tv =>
            {
                try { tv.Mac = Mac.Normalize(await parts.ReadMac(tv.Ip, ct)); }
                catch (Exception) { }
            }));
            ct.ThrowIfCancellationRequested();
        }

        private static async Task<string> Match(DiscoveryParts parts, List<Candidate> candidates, string want, CancellationToken ct)
        {
            string hit = null;
            using (var stop = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                await Task.WhenAll(candidates.Select(async c =>
                {
                    try
                    {
                        var tv = await parts.Describe(c, stop.Token);
                        if (tv == null) return;
                        var found = Mac.Normalize(await parts.ReadMac(tv.Ip, stop.Token));
                        if (found != want) return;
                        Interlocked.CompareExchange(ref hit, tv.Ip, null);
                        stop.Cancel();
                    }
                    catch (Exception) { }
                }));
            }
            ct.ThrowIfCancellationRequested();
            return hit;
        }
    }

    // When the app may search on its own.
    public static class AutoSearch
    {
        public static readonly TimeSpan MinGapAfterNoResponse = TimeSpan.FromSeconds(30);

        // Common rules: only for a TV whose MAC is saved, never two searches at once,
        // never while the Find dialog is open.
        public static bool CanStart(bool macSaved, bool searching, bool dialogOpen)
            => macSaved && !searching && !dialogOpen;

        // After "No response" or an HTTP failure on a click: also at most one search every 30 s.
        public static bool AfterNoResponse(bool macSaved, bool searching, bool dialogOpen, TimeSpan? sinceLast)
            => CanStart(macSaved, searching, dialogOpen) && (sinceLast == null || sinceLast.Value >= MinGapAfterNoResponse);
    }

    // The real network.
    public static class NetDiscovery
    {
        private const int DescriptionLimit = 256 * 1024;
        private static readonly TimeSpan ListenFor = TimeSpan.FromSeconds(3);
        private static readonly TimeSpan DescribeTimeout = TimeSpan.FromSeconds(1.5);
        private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);

        private static readonly HttpClient Http = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        public static DiscoveryParts Parts(TvClient tv) => new DiscoveryParts
        {
            Ssdp = SsdpSearch,
            Scan = Scan,
            Describe = Describe,
            ReadMac = (ip, ct) => tv.ReadMacAt(ip, TvClient.QuickTimeout, ct)
        };

        // Up Ethernet or Wi-Fi adapters with an IPv4 gateway, and their private IPv4 addresses.
        // No VPN, no public network, no 169.254.x.x.
        public static List<KeyValuePair<IPAddress, int>> LocalAddresses()
        {
            var list = new List<KeyValuePair<IPAddress, int>>();
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType != NetworkInterfaceType.Ethernet
                        && nic.NetworkInterfaceType != NetworkInterfaceType.Wireless80211
                        && nic.NetworkInterfaceType != NetworkInterfaceType.GigabitEthernet) continue;
                    var props = nic.GetIPProperties();
                    if (!props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork)) continue;
                    foreach (var ua in props.UnicastAddresses)
                        if (ScanRange.IsPrivate(ua.Address))
                            list.Add(new KeyValuePair<IPAddress, int>(ua.Address, ua.PrefixLength));
                }
            }
            catch (Exception) { }
            return list;
        }

        // M-SEARCH from every local address: two searches, repeated after 1 s (UDP can drop a
        // packet), answers heard for 3 s from the first search.
        private static async Task<List<Candidate>> SsdpSearch(CancellationToken ct)
        {
            var found = new ConcurrentDictionary<string, Candidate>();
            var addresses = LocalAddresses().Select(l => l.Key).Distinct().ToList();
            await Task.WhenAll(addresses.Select(a => SsdpOn(a, found, ct)));
            return found.Values.ToList();
        }

        private static async Task SsdpOn(IPAddress local, ConcurrentDictionary<string, Candidate> found, CancellationToken ct)
        {
            try
            {
                using (var udp = new UdpClient(new IPEndPoint(local, 0)))
                using (var listen = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 2);
                    try { udp.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, local.GetAddressBytes()); }
                    catch (Exception) { }
                    listen.CancelAfter(ListenFor);

                    var target = new IPEndPoint(IPAddress.Parse(Ssdp.Address), Ssdp.Port);
                    var sender = Task.Run(async () =>
                    {
                        for (int round = 0; round < 2; round++)
                        {
                            foreach (var st in Ssdp.SearchTargets)
                            {
                                var bytes = Encoding.ASCII.GetBytes(Ssdp.BuildSearch(st));
                                try { await udp.SendAsync(bytes, bytes.Length, target); }
                                catch (SocketException) { }
                            }
                            if (round == 0) await Task.Delay(1000, listen.Token);
                        }
                    });

                    while (!listen.IsCancellationRequested)
                    {
                        UdpReceiveResult res;
                        try { res = await udp.ReceiveAsync(listen.Token); }
                        catch (SocketException) { continue; }
                        var from = res.RemoteEndPoint.Address.ToString();
                        var candidate = Ssdp.ParseResponse(Encoding.ASCII.GetString(res.Buffer), from);
                        if (candidate != null) found.TryAdd(from, candidate);
                    }
                    try { await sender; } catch (OperationCanceledException) { }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        // Port 52323 on every address of the PC's private /24: 500 ms each, 64 at a time.
        private static async Task<List<Candidate>> Scan(CancellationToken ct)
        {
            var open = new ConcurrentBag<string>();
            using (var gate = new SemaphoreSlim(64))
            {
                await Task.WhenAll(ScanRange.ForInterfaces(LocalAddresses()).Select(async ip =>
                {
                    try { await gate.WaitAsync(ct); }
                    catch (OperationCanceledException) { return; }
                    try { if (await PortOpen(ip, 52323, ct)) open.Add(ip.ToString()); }
                    finally { gate.Release(); }
                }));
            }
            return open.Select(ip => new Candidate { Ip = ip, Location = Ssdp.ResolveLocation(null, ip) }).ToList();
        }

        private static async Task<bool> PortOpen(IPAddress ip, int port, CancellationToken ct)
        {
            try
            {
                using (var client = new TcpClient())
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(ConnectTimeout);
                    await client.ConnectAsync(ip, port, cts.Token);
                    return true;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // Reads dmr.xml: 1.5 s, at most 256 KB. Null when it is not a Sony TV or cannot be read.
        private static async Task<FoundTv> Describe(Candidate candidate, CancellationToken ct)
        {
            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(DescribeTimeout);
                    using (var req = new HttpRequestMessage(HttpMethod.Get, candidate.Location))
                    using (var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token))
                    {
                        if (!resp.IsSuccessStatusCode) return null;
                        using (var stream = await resp.Content.ReadAsStreamAsync(cts.Token))
                        {
                            var xml = await ReadLimited(stream, DescriptionLimit, cts.Token);
                            return xml == null ? null : DmrParser.Parse(xml, candidate.Ip);
                        }
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static async Task<string> ReadLimited(Stream stream, int limit, CancellationToken ct)
        {
            using (var buffer = new MemoryStream())
            {
                var chunk = new byte[8192];
                int n;
                while ((n = await stream.ReadAsync(chunk, 0, chunk.Length, ct)) > 0)
                {
                    if (buffer.Length + n > limit) return null;
                    buffer.Write(chunk, 0, n);
                }
                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }
    }
}
