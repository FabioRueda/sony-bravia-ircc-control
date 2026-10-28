using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

// User settings in %APPDATA%\SonyTvRemote\settings.json. The pairing cookie is
// stored encrypted with DPAPI (current user scope), never in plain text.
// Wake-on-LAN MAC and pairing belong to one TV, so they are kept per TV, keyed by IP.

namespace SonyTvRemote
{
    // What the app knows about one TV.
    public class TvData
    {
        public string Mac { get; set; }
        public string AuthCookieProtected { get; set; }
        public DateTime? AuthExpiresUtc { get; set; }
    }

    public class Settings
    {
        public const string DefaultIp = "192.168.1.100";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SonyTvRemote.auth");

        public string Language { get; set; } = "en-US";
        public string Ip { get; set; } = DefaultIp;
        // Identifies this PC to every TV, so it stays global.
        public string ClientId { get; set; }
        public Dictionary<string, TvData> Tvs { get; set; } = new Dictionary<string, TvData>();

        // 1.1.0 kept these at the top level for a single TV. Read only to migrate them.
        [JsonPropertyName("Mac"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyMac { get; set; }

        [JsonPropertyName("AuthCookieProtected"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LegacyAuthCookieProtected { get; set; }

        [JsonPropertyName("AuthExpiresUtc"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DateTime? LegacyAuthExpiresUtc { get; set; }

        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SonyTvRemote", "settings.json");

        public static Settings Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path));
                    if (s != null)
                    {
                        if (string.IsNullOrWhiteSpace(s.Ip)) s.Ip = DefaultIp;
                        if (string.IsNullOrWhiteSpace(s.Language)) s.Language = "en-US";
                        s.Tvs = Normalized(s.Tvs);
                        if (s.MigrateLegacy())
                        {
                            try { s.Save(path); } catch (Exception) { }
                        }
                        return s;
                    }
                }
            }
            catch (Exception)
            {
                // The defaults will overwrite the file on the next save: keep a copy first.
                try { File.Copy(path, path + ".bad", true); } catch (Exception) { }
            }
            return new Settings();
        }

        // Keys as Key() makes them (a hand-edited file may differ), without null entries.
        private static Dictionary<string, TvData> Normalized(Dictionary<string, TvData> tvs)
        {
            var result = new Dictionary<string, TvData>();
            if (tvs == null) return result;
            foreach (var kv in tvs)
            {
                if (kv.Value == null) continue;
                var key = Key(kv.Key);
                if (!result.ContainsKey(key)) result[key] = kv.Value;
            }
            return result;
        }

        // Moves the 1.1.0 top-level MAC and pairing to the TV at the saved IP.
        private bool MigrateLegacy()
        {
            if (LegacyMac == null && LegacyAuthCookieProtected == null && LegacyAuthExpiresUtc == null) return false;
            var tv = Entry(Ip);
            tv.Mac ??= LegacyMac;
            if (tv.AuthCookieProtected == null)
            {
                tv.AuthCookieProtected = LegacyAuthCookieProtected;
                tv.AuthExpiresUtc = LegacyAuthExpiresUtc;
            }
            LegacyMac = null;
            LegacyAuthCookieProtected = null;
            LegacyAuthExpiresUtc = null;
            return true;
        }

        public void Save(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(tmp, path, true);
        }

        // Generated once, then reused so the TV keeps recognizing this PC.
        public string EnsureClientId()
        {
            if (string.IsNullOrEmpty(ClientId)) ClientId = Guid.NewGuid().ToString();
            return ClientId;
        }

        // ---------- per TV ----------

        private static string Key(string ip) => (ip ?? "").Trim().ToLowerInvariant();

        // The entry for this IP, created if missing. Reads use Find so they don't add entries.
        public TvData Entry(string ip)
        {
            var key = Key(ip);
            if (!Tvs.TryGetValue(key, out var tv) || tv == null)
            {
                tv = new TvData();
                Tvs[key] = tv;
            }
            return tv;
        }

        private TvData Find(string ip) => Tvs.TryGetValue(Key(ip), out var tv) ? tv : null;

        public string GetMac(string ip) => Find(ip)?.Mac;

        public void SetMac(string ip, string mac) => Entry(ip).Mac = mac;

        public DateTime? GetAuthExpiresUtc(string ip) => Find(ip)?.AuthExpiresUtc;

        // Non-Windows (no DPAPI): cookies live only in memory for this session.
        private readonly Dictionary<string, string> _memoryCookies = new Dictionary<string, string>();

        public string GetCookie(string ip)
        {
            if (!OperatingSystem.IsWindows()) return _memoryCookies.TryGetValue(Key(ip), out var c) ? c : null;
            var stored = Find(ip)?.AuthCookieProtected;
            if (string.IsNullOrEmpty(stored)) return null;
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(stored), Entropy,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void SetCookie(string ip, string cookie, DateTime expiresUtc)
        {
            var tv = Entry(ip);
            // Without DPAPI (non-Windows) the cookie is not persisted at all.
            if (OperatingSystem.IsWindows())
                tv.AuthCookieProtected = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(cookie),
                    Entropy, DataProtectionScope.CurrentUser));
            else
                _memoryCookies[Key(ip)] = cookie;
            tv.AuthExpiresUtc = expiresUtc;
        }

        public void ClearCookie(string ip)
        {
            _memoryCookies.Remove(Key(ip));
            var tv = Find(ip);
            if (tv == null) return;
            tv.AuthCookieProtected = null;
            tv.AuthExpiresUtc = null;
        }
    }
}
