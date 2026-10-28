using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SonyTvRemote;
using Xunit;

namespace SonyTvRemote.Tests
{
    // Records requests and answers with queued replies.
    class StubHandler : HttpMessageHandler
    {
        public readonly List<(HttpRequestMessage Req, string Body)> Requests = new List<(HttpRequestMessage, string)>();
        public readonly Queue<Func<HttpResponseMessage>> Replies = new Queue<Func<HttpResponseMessage>>();

        public void Reply(HttpStatusCode code, string body, string setCookie = null)
            => Replies.Enqueue(() =>
            {
                var r = new HttpResponseMessage(code) { Content = new StringContent(body ?? "") };
                if (setCookie != null) r.Headers.TryAddWithoutValidation("Set-Cookie", setCookie);
                return r;
            });

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content == null ? null : await request.Content.ReadAsStringAsync();
            Requests.Add((request, body));
            if (Replies.Count == 0) throw new HttpRequestException("no reply queued");
            return Replies.Dequeue()();
        }
    }

    public class TvClientTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "SonyTvRemoteTests-" + Guid.NewGuid());
        private string FilePath => Path.Combine(_dir, "settings.json");
        private readonly StubHandler _stub = new StubHandler();
        private readonly Settings _settings;
        private readonly TvClient _tv;

        public TvClientTests()
        {
            _settings = Settings.Load(FilePath);
            _tv = new TvClient(_settings, FilePath, _stub) { Ip = "192.168.1.100" };
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static string Header(HttpRequestMessage r, string name)
            => r.Headers.TryGetValues(name, out var v) ? string.Join(",", v) : null;

        [Fact]
        public async Task ActRegister_without_pin_sends_the_spec_body_and_no_auth()
        {
            _stub.Reply(HttpStatusCode.Unauthorized, "{\"error\":[401,\"Unauthorized\"],\"id\":1}");
            var r = await _tv.ActRegister(null, false);

            Assert.Equal(401, r.Status);
            var (req, body) = _stub.Requests.Single();
            Assert.Equal("http://192.168.1.100/sony/accessControl", req.RequestUri.ToString());
            Assert.Null(req.Headers.Authorization);
            Assert.Null(Header(req, "Cookie"));

            using (var doc = JsonDocument.Parse(body))
            {
                var root = doc.RootElement;
                Assert.Equal("actRegister", root.GetProperty("method").GetString());
                var p = root.GetProperty("params");
                Assert.Equal("SonyTvRemote:" + _settings.ClientId, p[0].GetProperty("clientid").GetString());
                Assert.Equal("SonyTvRemote (" + Environment.MachineName + ")", p[0].GetProperty("nickname").GetString());
                Assert.Equal("private", p[0].GetProperty("level").GetString());
                Assert.Equal("yes", p[1][0].GetProperty("value").GetString());
                Assert.Equal("WOL", p[1][0].GetProperty("function").GetString());
            }
        }

        [Fact]
        public async Task ActRegister_with_pin_uses_basic_auth_with_empty_user()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[]}", "auth=abc; path=/sony/; max-age=1209600");
            var r = await _tv.ActRegister("1234", false);

            var req = _stub.Requests.Single().Req;
            Assert.Equal("Basic", req.Headers.Authorization.Scheme);
            Assert.Equal(Convert.ToBase64String(Encoding.ASCII.GetBytes(":1234")), req.Headers.Authorization.Parameter);
            Assert.True(_tv.SaveCookie(r));
            Assert.Equal("auth=abc", _settings.GetCookie());
            Assert.True(_tv.IsPaired);
        }

        [Fact]
        public async Task Scalar_sends_the_cookie_when_paired()
        {
            _settings.SetCookie("auth=abc", DateTime.UtcNow.AddDays(10));
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[]}");
            await _tv.Scalar("appControl", "setActiveApp", new Dictionary<string, string> { ["uri"] = "x" });

            var (req, body) = _stub.Requests.Single();
            Assert.Equal("auth=abc", Header(req, "Cookie"));
            Assert.Contains("\"params\":[{\"uri\":\"x\"}]", body);
        }

        [Fact]
        public async Task Bad_ip_gives_no_response_instead_of_throwing()
        {
            _tv.Ip = "192.168.1. 100";
            var r = await _tv.Ircc("AAAA");
            Assert.Equal(0, r.Status);
            Assert.Empty(_stub.Requests);
        }

        [Fact]
        public async Task Renew_is_skipped_while_more_than_two_days_remain()
        {
            _settings.SetCookie("auth=abc", DateTime.UtcNow.AddDays(5));
            await _tv.RenewIfNeeded();
            Assert.Empty(_stub.Requests);
        }

        [Fact]
        public async Task Renew_sends_the_cookie_without_pin_and_saves_the_new_one()
        {
            _settings.SetCookie("auth=old", DateTime.UtcNow.AddDays(1));
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[]}", "auth=new; path=/sony/; max-age=1209600");
            await _tv.RenewIfNeeded();

            var req = _stub.Requests.Single().Req;
            Assert.Equal("auth=old", Header(req, "Cookie"));
            Assert.Null(req.Headers.Authorization);
            Assert.Equal("auth=new", _settings.GetCookie());
            Assert.True(_settings.AuthExpiresUtc > DateTime.UtcNow.AddDays(13));
            if (OperatingSystem.IsWindows())   // persisted only with DPAPI
                Assert.Equal("auth=new", Settings.Load(FilePath).GetCookie());
        }

        [Fact]
        public async Task Failed_renewal_before_expiry_keeps_the_cookie()
        {
            _settings.SetCookie("auth=old", DateTime.UtcNow.AddDays(1));
            _stub.Reply(HttpStatusCode.Unauthorized, "");
            await _tv.RenewIfNeeded();
            Assert.Equal("auth=old", _settings.GetCookie());
            Assert.True(_tv.IsPaired);
        }

        [Fact]
        public async Task Expired_cookie_is_dropped_without_asking_the_tv()
        {
            _settings.SetCookie("auth=old", DateTime.UtcNow.AddMinutes(-1));
            await _tv.RenewIfNeeded();
            Assert.Empty(_stub.Requests);
            Assert.Null(_settings.GetCookie());
            Assert.False(_tv.IsPaired);
        }
    }
}
