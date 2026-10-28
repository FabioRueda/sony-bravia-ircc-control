using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;

// UI-free logic: version, Wake-on-LAN, URL handling, IRCC codes, Scalar replies,
// the auth cookie, and the power-on sequence. Covered by tests/TvRemote.Tests.

namespace SonyTvRemote
{
    public static class AppInfo
    {
        // Comes from <Version> in TvRemote.csproj.
        public static string Version => typeof(AppInfo).Assembly.GetName().Version.ToString(3);

        public const string RepoUrl = "https://github.com/FabioRueda/sony-bravia-ircc-control";
    }

    public static class WakeOnLan
    {
        public static byte[] BuildMagicPacket(string mac)
        {
            var parts = (mac ?? "").Split(':', '-');
            if (parts.Length != 6) throw new FormatException("MAC must have 6 bytes: " + mac);
            var bytes = parts.Select(p => byte.Parse(p, NumberStyles.HexNumber)).ToArray();

            var packet = new byte[6 + 16 * 6];
            for (int i = 0; i < 6; i++) packet[i] = 0xFF;
            for (int i = 0; i < 16; i++) Array.Copy(bytes, 0, packet, 6 + i * 6, 6);
            return packet;
        }

        public static IPAddress Broadcast(IPAddress ip, int prefixLength)
        {
            var b = ip.GetAddressBytes();
            uint addr = (uint)(b[0] << 24 | b[1] << 16 | b[2] << 8 | b[3]);
            uint mask = prefixLength == 0 ? 0 : uint.MaxValue << (32 - prefixLength);
            uint bc = addr | ~mask;
            return new IPAddress(new[] { (byte)(bc >> 24), (byte)(bc >> 16), (byte)(bc >> 8), (byte)bc });
        }

        // 255.255.255.255 plus the subnet broadcast of every active adapter with a gateway.
        public static List<IPAddress> Targets()
        {
            var list = new List<IPAddress> { IPAddress.Broadcast };
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                var props = nic.GetIPProperties();
                if (!props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork)) continue;
                foreach (var ua in props.UnicastAddresses)
                {
                    if (ua.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var bc = Broadcast(ua.Address, ua.PrefixLength);
                    if (!list.Contains(bc)) list.Add(bc);
                }
            }
            return list;
        }

        public static async Task SendAsync(string mac)
        {
            var packet = BuildMagicPacket(mac);
            using (var udp = new UdpClient())
            {
                udp.EnableBroadcast = true;
                // One unreachable target must not stop the others.
                foreach (var target in Targets())
                {
                    try { await udp.SendAsync(packet, packet.Length, new IPEndPoint(target, 9)); }
                    catch (SocketException) { }
                }
            }
        }
    }

    public static class UrlHelper
    {
        // A bare IPv4 address or host name (no scheme, port or spaces).
        public static bool IsValidHost(string host)
        {
            var kind = Uri.CheckHostName(host ?? "");
            return kind == UriHostNameType.IPv4 || kind == UriHostNameType.Dns;
        }

        // Adds https:// when the user typed no scheme. Returns null when there is no URL.
        public static string Normalize(string input)
        {
            var url = (input ?? "").Trim();
            if (url.Length == 0) return null;
            bool hasScheme = url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                          || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            if (!hasScheme) return "https://" + url;
            return url.Substring(url.IndexOf("://", StringComparison.Ordinal) + 3).Length == 0 ? null : url;
        }
    }

    public static class IrccCodes
    {
        // Official codes from system/getRemoteControllerInfo on a KDL-42W805A (2026-10-28).
        // Used when the TV's own list can't be fetched.
        public static readonly IReadOnlyDictionary<string, string> Fallback = new Dictionary<string, string>
        {
            ["PowerOff"]    = "AAAAAQAAAAEAAAAvAw==",
            ["Input"]       = "AAAAAQAAAAEAAAAlAw==",
            ["Display"]     = "AAAAAQAAAAEAAAA6Aw==",
            ["Home"]        = "AAAAAQAAAAEAAABgAw==",
            ["Options"]     = "AAAAAgAAAJcAAAA2Aw==",
            ["Return"]      = "AAAAAgAAAJcAAAAjAw==",
            ["Up"]          = "AAAAAQAAAAEAAAB0Aw==",
            ["Down"]        = "AAAAAQAAAAEAAAB1Aw==",
            ["Right"]       = "AAAAAQAAAAEAAAAzAw==",
            ["Left"]        = "AAAAAQAAAAEAAAA0Aw==",
            ["Confirm"]     = "AAAAAQAAAAEAAABlAw==",
            ["VolumeUp"]    = "AAAAAQAAAAEAAAASAw==",
            ["VolumeDown"]  = "AAAAAQAAAAEAAAATAw==",
            ["Mute"]        = "AAAAAQAAAAEAAAAUAw==",
            ["ChannelUp"]   = "AAAAAQAAAAEAAAAQAw==",
            ["ChannelDown"] = "AAAAAQAAAAEAAAARAw==",

            // Added in 1.2.0, read from the same list (KDL-42W805A, 2026-10-28).
            ["Num1"]        = "AAAAAQAAAAEAAAAAAw==",
            ["Num2"]        = "AAAAAQAAAAEAAAABAw==",
            ["Num3"]        = "AAAAAQAAAAEAAAACAw==",
            ["Num4"]        = "AAAAAQAAAAEAAAADAw==",
            ["Num5"]        = "AAAAAQAAAAEAAAAEAw==",
            ["Num6"]        = "AAAAAQAAAAEAAAAFAw==",
            ["Num7"]        = "AAAAAQAAAAEAAAAGAw==",
            ["Num8"]        = "AAAAAQAAAAEAAAAHAw==",
            ["Num9"]        = "AAAAAQAAAAEAAAAIAw==",
            ["Num0"]        = "AAAAAQAAAAEAAAAJAw==",
            ["Enter"]       = "AAAAAQAAAAEAAAALAw==",
            ["Red"]         = "AAAAAgAAAJcAAAAlAw==",
            ["Green"]       = "AAAAAgAAAJcAAAAmAw==",
            ["Yellow"]      = "AAAAAgAAAJcAAAAnAw==",
            ["Blue"]        = "AAAAAgAAAJcAAAAkAw==",
            ["Play"]        = "AAAAAgAAAJcAAAAaAw==",
            ["Pause"]       = "AAAAAgAAAJcAAAAZAw==",
            ["Stop"]        = "AAAAAgAAAJcAAAAYAw==",
            ["Rewind"]      = "AAAAAgAAAJcAAAAbAw==",
            ["Forward"]     = "AAAAAgAAAJcAAAAcAw==",
            ["Prev"]        = "AAAAAgAAAJcAAAA8Aw==",
            ["Next"]        = "AAAAAgAAAJcAAAA9Aw==",
            ["SubTitle"]    = "AAAAAgAAAJcAAAAoAw==",
            ["Audio"]       = "AAAAAQAAAAEAAAAXAw==",
            ["Wide"]        = "AAAAAgAAAKQAAAA9Aw==",
            ["Exit"]        = "AAAAAQAAAAEAAABjAw==",
            ["PicOff"]      = "AAAAAQAAAAEAAAA+Aw==",
            ["SyncMenu"]    = "AAAAAgAAABoAAABYAw==",
            ["EPG"]         = "AAAAAgAAAKQAAABbAw==",   // the Guide button
            ["GGuide"]      = "AAAAAQAAAAEAAAAOAw==",   // Guide on a TV with no EPG code

            // Added in 1.3.0: the ANT key (Antenna / Cable). Checked on the KDL-42W805A in front of the TV.
            ["Analog"]      = "AAAAAgAAAHcAAAANAw==",
        };

        // Not in the TV's list; the standard BRAVIA codes.
        public const string PowerOn = "AAAAAQAAAAEAAAAuAw==";
        public const string Hdmi1 = "AAAAAgAAABoAAABaAw==";
        public const string Hdmi2 = "AAAAAgAAABoAAABbAw==";
        public const string Hdmi3 = "AAAAAgAAABoAAABcAw==";
        public const string Hdmi4 = "AAAAAgAAABoAAABdAw==";

        // Reads the name -> code list from a getRemoteControllerInfo reply; null if unusable.
        public static Dictionary<string, string> Parse(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (!doc.RootElement.TryGetProperty("result", out var result)) return null;
                    if (result.ValueKind != JsonValueKind.Array || result.GetArrayLength() < 2) return null;
                    var map = new Dictionary<string, string>();
                    foreach (var item in result[1].EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var n) && item.TryGetProperty("value", out var v))
                            map[n.GetString()] = v.GetString();
                    }
                    return map.Count > 0 ? map : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string Resolve(IReadOnlyDictionary<string, string> fromTv, string name)
        {
            if (fromTv != null && fromTv.TryGetValue(name, out var code)) return code;
            return Fallback[name];
        }

        // The Guide button: "EPG" on the TV's list, else "GGuide", else the built-in EPG code.
        public static string ResolveGuide(IReadOnlyDictionary<string, string> fromTv)
        {
            if (fromTv != null)
            {
                if (fromTv.TryGetValue("EPG", out var epg)) return epg;
                if (fromTv.TryGetValue("GGuide", out var gguide)) return gguide;
            }
            return Fallback["EPG"];
        }
    }

    public enum ScalarOutcome { Ok, NoResponse, NotPaired, NotSupported, TvError, HttpFailed }

    public static class ScalarReply
    {
        // The TV refuses unpaired calls with HTTP 401/403, an "auth_url" field, or error 401/403.
        public static bool NeedsAuth(int status, string body)
        {
            if (status == 401 || status == 403) return true;
            if (body != null && body.Contains("\"auth_url\"")) return true;
            var code = ErrorCode(body);
            return code == 401 || code == 403;
        }

        public static int? ErrorCode(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.TryGetProperty("error", out var err)
                        && err.ValueKind == JsonValueKind.Array && err.GetArrayLength() > 0
                        && err[0].TryGetInt32(out var code))
                        return code;
                }
            }
            catch (Exception) { }
            return null;
        }

        // Error 12 (no such method), 14 (unsupported version) and 15 (unsupported operation), or an
        // unknown service/method path (HTTP 404). Pairing errors are never "unsupported".
        public static bool IsUnsupported(int status, string body)
        {
            if (NeedsAuth(status, body)) return false;
            if (status == 404) return true;
            var code = ErrorCode(body);
            return code == 12 || code == 14 || code == 15;
        }

        // One verdict per reply, so every feature reports problems the same way.
        public static ScalarOutcome Classify(int status, string body)
        {
            if (status == 0) return ScalarOutcome.NoResponse;
            if (NeedsAuth(status, body)) return ScalarOutcome.NotPaired;
            if (IsUnsupported(status, body)) return ScalarOutcome.NotSupported;
            if (ErrorCode(body) != null) return ScalarOutcome.TvError;
            return status == 200 ? ScalarOutcome.Ok : ScalarOutcome.HttpFailed;
        }

        // Status of one entry of appControl/getApplicationStatusList ("textInput", ...); null if absent.
        public static string AppStatus(string body, string name)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (!doc.RootElement.TryGetProperty("result", out var r) || r.GetArrayLength() == 0) return null;
                    foreach (var item in r[0].EnumerateArray())
                    {
                        if (item.TryGetProperty("name", out var n) && n.GetString() == name
                            && item.TryGetProperty("status", out var s))
                            return s.GetString();
                    }
                }
            }
            catch (Exception) { }
            return null;
        }

        // The string from system/getCurrentTime; null when the clock isn't set or the reply is unusable.
        public static string CurrentTime(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.TryGetProperty("result", out var r) && r.GetArrayLength() > 0
                        && r[0].ValueKind == JsonValueKind.String)
                        return r[0].GetString();
                }
            }
            catch (Exception) { }
            return null;
        }

        public static string PowerStatus(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.TryGetProperty("result", out var r) && r.GetArrayLength() > 0
                        && r[0].TryGetProperty("status", out var s))
                        return s.GetString();
                }
            }
            catch (Exception) { }
            return null;
        }

        // MAC for Wake-on-LAN from system/getSystemSupportedFunction (item with option "WOL").
        public static string WolMac(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (!doc.RootElement.TryGetProperty("result", out var r) || r.GetArrayLength() == 0) return null;
                    foreach (var item in r[0].EnumerateArray())
                    {
                        if (item.TryGetProperty("option", out var o) && o.GetString() == "WOL"
                            && item.TryGetProperty("value", out var v))
                            return v.GetString();
                    }
                }
            }
            catch (Exception) { }
            return null;
        }
    }

    public static class AuthCookie
    {
        public static readonly TimeSpan RenewBefore = TimeSpan.FromDays(2);
        private const long MaxAgeLimitSeconds = 366L * 24 * 3600;

        // Parses "auth=...; path=/sony/; max-age=1209600; expires=..." from Set-Cookie.
        public static bool TryParse(string setCookie, DateTime nowUtc, out string cookie, out DateTime expiresUtc)
        {
            cookie = null;
            expiresUtc = default;
            if (string.IsNullOrEmpty(setCookie)) return false;

            var parts = setCookie.Split(';').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            var auth = parts.FirstOrDefault(p => p.StartsWith("auth=", StringComparison.Ordinal));
            if (auth == null || auth.Length <= 5) return false;

            DateTime? exp = null;
            foreach (var p in parts)
            {
                if (p.StartsWith("max-age=", StringComparison.OrdinalIgnoreCase)
                    && long.TryParse(p.Substring(8), out var secs))
                    exp = nowUtc.AddSeconds(Math.Clamp(secs, 0, MaxAgeLimitSeconds));
            }
            if (exp == null)
            {
                var e = parts.FirstOrDefault(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
                if (e != null && DateTime.TryParseExact(e.Substring(8), "ddd, dd-MMM-yyyy HH:mm:ss 'GMT'",
                        CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var d))
                    exp = d;
            }

            cookie = auth;
            expiresUtc = exp ?? nowUtc.AddDays(14);
            return true;
        }

        public static bool NeedsRenewal(DateTime expiresUtc, DateTime nowUtc) => expiresUtc - nowUtc < RenewBefore;
    }

    public static class PowerOnSequence
    {
        private static readonly TimeSpan Step = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan FallbackAfter = TimeSpan.FromSeconds(6);
        private static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(20);

        // Wake-on-LAN, poll every 2 s up to 6 s; still in standby -> setPowerStatus (when paired)
        // and IRCC Power ON; keep polling up to 20 s. Stops as soon as the TV reports "active".
        // Times are measured with elapsed(), so slow polls can't stretch the run.
        // On the KDL-42W805A only Wake-on-LAN actually turns the TV on.
        public static async Task<bool> RunAsync(Func<Task> sendWol, Func<Task<bool>> isActive,
            Func<Task> setPowerStatus, Func<Task> irccPowerOn, Func<TimeSpan, Task> delay, Func<TimeSpan> elapsed)
        {
            await Quiet(sendWol);

            bool fallbacksSent = false;
            while (true)
            {
                await delay(Step);
                if (await Active(isActive)) return true;
                if (!fallbacksSent && elapsed() >= FallbackAfter)
                {
                    fallbacksSent = true;
                    if (setPowerStatus != null) await Quiet(setPowerStatus);
                    await Quiet(irccPowerOn);
                }
                if (elapsed() >= GiveUpAfter) return false;
            }
        }

        private static async Task Quiet(Func<Task> action)
        {
            try { await action(); } catch (Exception) { }
        }

        private static async Task<bool> Active(Func<Task<bool>> isActive)
        {
            try { return await isActive(); } catch (Exception) { return false; }
        }
    }
}
