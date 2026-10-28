using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

// HTTP calls to the TV: IRCC-IP (SOAP) and the Scalar Web API (JSON-RPC), plus
// PIN pairing. The pairing cookie is sent on every Scalar call when present.

namespace SonyTvRemote
{
    public class TvReply
    {
        public int Status;       // 0 = no response (or invalid address)
        public string Ip;        // the TV the request went to (the current IP may change meanwhile)
        public string Body;
        public string SetCookie;

        public bool Ok => Status == 200 && ScalarReply.ErrorCode(Body) == null;
    }

    public class TvClient
    {
        public static readonly TimeSpan NormalTimeout = TimeSpan.FromSeconds(5);
        // For status polls and first-contact info, so an unreachable TV doesn't stall the UI.
        public static readonly TimeSpan QuickTimeout = TimeSpan.FromSeconds(1.5);

        private readonly HttpClient _http;
        private readonly Settings _settings;
        private readonly string _settingsPath;

        public string Ip { get; set; }

        public TvClient(Settings settings, string settingsPath, HttpMessageHandler handler = null)
        {
            _settings = settings;
            _settingsPath = settingsPath;
            // Per-request timeouts come from a CancellationToken in Send.
            _http = new HttpClient(handler ?? new HttpClientHandler { UseCookies = false })
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        // MAC and pairing always refer to the TV at the current IP.
        public bool IsPaired => _settings.GetCookie(Ip) != null
                             && _settings.GetAuthExpiresUtc(Ip) is DateTime exp && exp > DateTime.UtcNow;

        public DateTime? PairedUntilUtc => _settings.GetAuthExpiresUtc(Ip);

        public string Mac => _settings.GetMac(Ip);

        // Asks the TV for its Wake-on-LAN MAC (system/getSystemSupportedFunction, no pairing)
        // and saves it for the current IP. Null when the TV doesn't answer or has none.
        public async Task<string> RefreshMac(TimeSpan timeout)
        {
            var reply = await ScalarWithin(timeout, "system", "getSystemSupportedFunction", new object[0]);
            var mac = ScalarReply.WolMac(reply.Body);
            if (mac == null) return null;
            if (mac != _settings.GetMac(reply.Ip))
            {
                _settings.SetMac(reply.Ip, mac);
                TrySave();
            }
            return mac;
        }

        public Task<TvReply> Ircc(string code)
        {
            var soap =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                "<s:Body><u:X_SendIRCC xmlns:u=\"urn:schemas-sony-com:service:IRCC:1\">" +
                "<IRCCCode>" + code + "</IRCCCode></u:X_SendIRCC></s:Body></s:Envelope>";

            return Send("IRCC", NormalTimeout, req =>
            {
                req.Content = new StringContent(soap, Encoding.UTF8, "text/xml");
                req.Headers.TryAddWithoutValidation("SOAPAction", "\"urn:schemas-sony-com:service:IRCC:1#X_SendIRCC\"");
            });
        }

        public Task<TvReply> Scalar(string service, string method, params object[] parameters)
            => ScalarWithin(NormalTimeout, service, method, parameters);

        public Task<TvReply> QuickScalar(string service, string method, params object[] parameters)
            => ScalarWithin(QuickTimeout, service, method, parameters);

        private Task<TvReply> ScalarWithin(TimeSpan timeout, string service, string method, object[] parameters)
        {
            var json = Json(method, parameters);
            var cookie = _settings.GetCookie(Ip);
            return Send(service, timeout, req =>
            {
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                if (cookie != null) req.Headers.TryAddWithoutValidation("Cookie", cookie);
            });
        }

        // ---- 1.2.0 calls. Reads need no pairing; the writes do (see README). ----

        public Task<TvReply> GetVolume() => QuickScalar("audio", "getVolumeInformation");

        public Task<TvReply> SetVolume(int volume)
            => Scalar("audio", "setAudioVolume",
                new Dictionary<string, string> { ["target"] = "speaker", ["volume"] = volume.ToString(CultureInfo.InvariantCulture) });

        public Task<TvReply> GetInputs() => QuickScalar("avContent", "getCurrentExternalInputsStatus");

        public Task<TvReply> PlayInput(string uri)
            => Scalar("avContent", "setPlayContent", new Dictionary<string, string> { ["uri"] = uri });

        public Task<TvReply> GetAppStatus() => QuickScalar("appControl", "getApplicationStatusList");

        // The TV's signature is setTextForm(string): the text itself is the only param.
        public Task<TvReply> SendText(string text) => Scalar("appControl", "setTextForm", text);

        public Task<TvReply> GetColorLayout() => QuickScalar("system", "getColorKeysLayout");

        public Task<TvReply> GetCurrentTime() => QuickScalar("system", "getCurrentTime");

        public Task<TvReply> SetClock(DateTimeOffset now, TimeZoneInfo zone)
            => Scalar("system", "setCurrentTime", TvClock.Params(now, zone));

        // accessControl/actRegister. No PIN and no cookie: the TV shows a PIN and answers 401.
        // With the PIN: Basic auth with an empty user. With the cookie: renews without a PIN.
        public Task<TvReply> ActRegister(string pin, bool withCookie)
        {
            var clientId = _settings.EnsureClientId();
            var json = Json("actRegister", new object[]
            {
                new Dictionary<string, string>
                {
                    ["clientid"] = "SonyTvRemote:" + clientId,
                    ["nickname"] = "SonyTvRemote (" + Environment.MachineName + ")",
                    ["level"] = "private"
                },
                new object[] { new Dictionary<string, string> { ["value"] = "yes", ["function"] = "WOL" } }
            });
            var cookie = withCookie ? _settings.GetCookie(Ip) : null;
            return Send("accessControl", NormalTimeout, req =>
            {
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                if (pin != null)
                    req.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                        Convert.ToBase64String(Encoding.ASCII.GetBytes(":" + pin)));
                if (cookie != null) req.Headers.TryAddWithoutValidation("Cookie", cookie);
            });
        }

        // Keeps the cookie from a successful actRegister. Returns false if there is none.
        // A failed save to disk still leaves the cookie usable for this session.
        public bool SaveCookie(TvReply reply)
        {
            if (reply.Status != 200) return false;
            if (!AuthCookie.TryParse(reply.SetCookie, DateTime.UtcNow, out var cookie, out var expires)) return false;
            _settings.SetCookie(reply.Ip, cookie, expires);
            TrySave();
            return true;
        }

        // Renews the cookie without a PIN when less than 2 days remain. An expired cookie is
        // dropped without asking the TV (it would show a PIN nobody asked for).
        public async Task RenewIfNeeded()
        {
            var exp = _settings.GetAuthExpiresUtc(Ip);
            if (_settings.GetCookie(Ip) == null || exp == null) return;
            if (exp.Value <= DateTime.UtcNow)
            {
                _settings.ClearCookie(Ip);
                TrySave();
                return;
            }
            if (!AuthCookie.NeedsRenewal(exp.Value, DateTime.UtcNow)) return;

            SaveCookie(await ActRegister(null, true));
        }

        private void TrySave()
        {
            try { _settings.Save(_settingsPath); } catch (Exception) { }
        }

        private static string Json(string method, object[] parameters)
            => JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["method"] = method,
                ["id"] = 1,
                ["version"] = "1.0",
                ["params"] = parameters
            });

        private async Task<TvReply> Send(string path, TimeSpan timeout, Action<HttpRequestMessage> fill)
        {
            // Taken before any await: replies are stored under the TV that was asked.
            var ip = Ip;
            if (!UrlHelper.IsValidHost(ip)) return new TvReply { Status = 0, Ip = ip };
            try
            {
                using (var req = new HttpRequestMessage(HttpMethod.Post, "http://" + ip + "/sony/" + path))
                using (var cts = new CancellationTokenSource(timeout))
                {
                    fill(req);
                    using (var resp = await _http.SendAsync(req, cts.Token))
                    {
                        var reply = new TvReply
                        {
                            Status = (int)resp.StatusCode,
                            Ip = ip,
                            Body = await resp.Content.ReadAsStringAsync()
                        };
                        if (resp.Headers.TryGetValues("Set-Cookie", out var cookies))
                            reply.SetCookie = string.Join("; ", cookies);
                        return reply;
                    }
                }
            }
            catch (Exception)
            {
                return new TvReply { Status = 0, Ip = ip };
            }
        }
    }
}
