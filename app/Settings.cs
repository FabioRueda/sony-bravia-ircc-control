using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// User settings in %APPDATA%\SonyTvRemote\settings.json. The pairing cookie is
// stored encrypted with DPAPI (current user scope), never in plain text.

namespace SonyTvRemote
{
    public class Settings
    {
        public const string DefaultIp = "192.168.1.100";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("SonyTvRemote.auth");

        public string Language { get; set; } = "en-US";
        public string Ip { get; set; } = DefaultIp;
        public string ClientId { get; set; }
        public string Mac { get; set; }
        public string AuthCookieProtected { get; set; }
        public DateTime? AuthExpiresUtc { get; set; }

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
                        return s;
                    }
                }
            }
            catch (Exception) { }
            return new Settings();
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

        // Non-Windows (no DPAPI): the cookie lives only in memory for this session.
        private string _memoryCookie;

        public string GetCookie()
        {
            if (!OperatingSystem.IsWindows()) return _memoryCookie;
            if (string.IsNullOrEmpty(AuthCookieProtected)) return null;
            try
            {
                var data = ProtectedData.Unprotect(Convert.FromBase64String(AuthCookieProtected), Entropy,
                    DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(data);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public void SetCookie(string cookie, DateTime expiresUtc)
        {
            // Without DPAPI (non-Windows) the cookie is not persisted at all.
            if (OperatingSystem.IsWindows())
                AuthCookieProtected = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(cookie),
                    Entropy, DataProtectionScope.CurrentUser));
            else
                _memoryCookie = cookie;
            AuthExpiresUtc = expiresUtc;
        }

        public void ClearCookie()
        {
            AuthCookieProtected = null;
            _memoryCookie = null;
            AuthExpiresUtc = null;
        }
    }
}
