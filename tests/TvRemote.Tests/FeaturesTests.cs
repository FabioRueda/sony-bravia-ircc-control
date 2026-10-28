using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using SonyTvRemote;
using Xunit;

namespace SonyTvRemote.Tests
{
    public class InputListTests
    {
        // Shape of avContent/getCurrentExternalInputsStatus on a KDL-42W805A.
        private const string Body =
            "{\"id\":1,\"result\":[[" +
            "{\"connection\":true,\"icon\":\"meta:hdmi\",\"label\":\"\",\"title\":\"HDMI 1\",\"uri\":\"extInput:hdmi?port=1\"}," +
            "{\"connection\":false,\"icon\":\"meta:hdmi\",\"label\":\"\",\"title\":\"HDMI 2/MHL\",\"uri\":\"extInput:hdmi?port=2\"}," +
            "{\"connection\":false,\"icon\":\"meta:composite\",\"label\":\"\",\"title\":\"Vídeo 1\",\"uri\":\"extInput:composite?port=1\"}," +
            "{\"connection\":true,\"icon\":\"meta:wifidisplay\",\"label\":\"\",\"title\":\"Screen mirroring\",\"uri\":\"extInput:widi?port=1\"}" +
            "]]}";

        [Fact]
        public void Parse_keeps_the_title_uri_and_connection_the_tv_reports_in_order()
        {
            var list = InputList.Parse(Body);
            Assert.Equal(new[] { "HDMI 1", "HDMI 2/MHL", "Vídeo 1", "Screen mirroring" }, list.Select(i => i.Title).ToArray());
            Assert.Equal("extInput:hdmi?port=2", list[1].Uri);
            Assert.Equal(new[] { true, false, false, true }, list.Select(i => i.Connected).ToArray());
        }

        [Fact]
        public void Parse_skips_entries_without_a_uri_and_uses_the_uri_when_the_title_is_empty()
        {
            var body = "{\"id\":1,\"result\":[[{\"title\":\"No uri\",\"connection\":true},{\"title\":\"\",\"uri\":\"extInput:hdmi?port=3\",\"connection\":false}]]}";
            var list = InputList.Parse(body);
            var only = Assert.Single(list);
            Assert.Equal("extInput:hdmi?port=3", only.Title);
            Assert.False(only.Connected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{\"error\":[7,\"x\"],\"id\":1}")]
        [InlineData("{\"id\":1,\"result\":[]}")]
        public void Parse_gives_an_empty_list_for_unusable_replies(string body)
            => Assert.Empty(InputList.Parse(body));

        [Theory]
        [InlineData("extInput:hdmi?port=1", IrccCodes.Hdmi1)]
        [InlineData("extInput:hdmi?port=2", IrccCodes.Hdmi2)]
        [InlineData("extInput:hdmi?port=3", IrccCodes.Hdmi3)]
        [InlineData("extInput:hdmi?port=4", IrccCodes.Hdmi4)]
        public void Hdmi_inputs_have_an_ircc_fallback(string uri, string code)
            => Assert.Equal(code, InputList.IrccFallback(uri));

        [Theory]
        [InlineData("extInput:hdmi?port=5")]
        [InlineData("extInput:composite?port=1")]
        [InlineData("extInput:widi?port=1")]
        [InlineData("")]
        [InlineData(null)]
        public void Other_inputs_have_no_ircc_fallback(string uri)
            => Assert.Null(InputList.IrccFallback(uri));
    }

    public class VolumeInfoTests
    {
        private const string Body =
            "{\"id\":1,\"result\":[[{\"maxVolume\":100,\"minVolume\":0,\"mute\":true,\"target\":\"speaker\",\"volume\":16}," +
            "{\"maxVolume\":50,\"minVolume\":0,\"mute\":false,\"target\":\"headphone\",\"volume\":30}]]}";

        [Fact]
        public void Parse_reads_the_speaker_entry()
        {
            var v = VolumeInfo.Parse(Body);
            Assert.Equal(16, v.Volume);
            Assert.Equal(100, v.Max);
            Assert.True(v.Mute);
        }

        [Fact]
        public void Parse_uses_the_first_entry_when_there_is_no_speaker()
        {
            var v = VolumeInfo.Parse("{\"id\":1,\"result\":[[{\"maxVolume\":60,\"minVolume\":0,\"mute\":false,\"target\":\"headphone\",\"volume\":7}]]}");
            Assert.Equal(7, v.Volume);
            Assert.Equal(60, v.Max);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("garbage")]
        [InlineData("{\"error\":[403,\"x\"],\"id\":1}")]
        [InlineData("{\"id\":1,\"result\":[[]]}")]
        public void Parse_gives_null_for_unusable_replies(string body)
            => Assert.Null(VolumeInfo.Parse(body));

        [Theory]
        [InlineData(-5, 100, 0)]
        [InlineData(0, 100, 0)]
        [InlineData(37, 100, 37)]
        [InlineData(100, 100, 100)]
        [InlineData(101, 100, 100)]
        [InlineData(250, 60, 60)]
        public void Clamp_keeps_the_volume_between_zero_and_the_maximum(int value, int max, int expected)
            => Assert.Equal(expected, VolumeInfo.Clamp(value, max));
    }

    public class ColorKeysTests
    {
        [Fact]
        public void Layout_is_read_from_the_reply()
            => Assert.Equal("RGYB", ColorKeys.ParseLayout("{\"id\":1,\"result\":[{\"colorKeysLayout\":\"RGYB\"}]}"));

        [Theory]
        [InlineData(null)]
        [InlineData("garbage")]
        [InlineData("{\"error\":[12,\"No such method\"],\"id\":1}")]
        public void Layout_is_null_for_unusable_replies(string body)
            => Assert.Null(ColorKeys.ParseLayout(body));

        [Fact]
        public void Order_follows_the_layout_letters()
        {
            Assert.Equal(new[] { "Red", "Green", "Yellow", "Blue" }, ColorKeys.Order("RGYB"));
            Assert.Equal(new[] { "Blue", "Yellow", "Green", "Red" }, ColorKeys.Order("BYGR"));
            Assert.Equal(new[] { "Red", "Green", "Blue", "Yellow" }, ColorKeys.Order("rgby"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("RGY")]
        [InlineData("RGYBX")]
        [InlineData("RRGB")]
        [InlineData("ABCD")]
        public void Order_defaults_to_RGYB_for_an_invalid_layout(string layout)
            => Assert.Equal(new[] { "Red", "Green", "Yellow", "Blue" }, ColorKeys.Order(layout));
    }

    public class TvClockTests
    {
        private static readonly TimeZoneInfo Brazil = TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(-3), "t", "t");

        [Fact]
        public void Params_send_the_time_in_utc_with_a_Z_suffix_and_the_pc_offset()
        {
            // 09:42:21 in UTC-3 is 12:42:21 UTC. The TV only accepts the "Z" form (2026-10-28, KDL-42W805A).
            var now = new DateTimeOffset(2026, 10, 28, 9, 42, 21, TimeSpan.FromHours(-3));
            var p = TvClock.Params(now, Brazil);
            Assert.Equal("2026-10-28T12:42:21Z", p["dateTime"]);
            Assert.Equal(-180, p["timeZoneOffsetMinute"]);
            Assert.Equal(0, p["dstOffsetMinute"]);
        }

        [Fact]
        public void Params_split_the_standard_offset_from_the_daylight_saving_offset()
        {
            var rule = TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
                new DateTime(2020, 1, 1), new DateTime(2030, 12, 31), TimeSpan.FromHours(1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1), 1, 1),
                TimeZoneInfo.TransitionTime.CreateFixedDateRule(new DateTime(1, 1, 1, 23, 59, 59), 12, 31));
            var tz = TimeZoneInfo.CreateCustomTimeZone("d", TimeSpan.FromHours(1), "d", "d", "d", new[] { rule });
            var now = new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.FromHours(2));
            var p = TvClock.Params(now, tz);
            Assert.Equal("2026-07-01T10:00:00Z", p["dateTime"]);
            Assert.Equal(60, p["timeZoneOffsetMinute"]);
            Assert.Equal(60, p["dstOffsetMinute"]);
        }

        [Fact]
        public void Params_are_independent_of_the_pc_culture()
        {
            var old = System.Globalization.CultureInfo.CurrentCulture;
            try
            {
                System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("ar-SA");
                var p = TvClock.Params(new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), TimeZoneInfo.Utc);
                Assert.Equal("2026-01-02T03:04:05Z", p["dateTime"]);
            }
            finally { System.Globalization.CultureInfo.CurrentCulture = old; }
        }
    }

    public class NewIrccCodesTests
    {
        public static readonly string[] Wanted =
        {
            "Num0", "Num1", "Num2", "Num3", "Num4", "Num5", "Num6", "Num7", "Num8", "Num9", "Enter",
            "Red", "Green", "Yellow", "Blue",
            "Play", "Pause", "Stop", "Rewind", "Forward", "Prev", "Next",
            "SubTitle", "Audio", "Wide", "Exit", "PicOff", "SyncMenu", "EPG", "GGuide",
        };

        public static readonly string[] Excluded =
            { "BS", "CS", "Ddata", "acTVila", "Digital", "Netflix", "InternetVideo", "InternetWidgets" };

        [Fact]
        public void Fallback_has_every_new_key_with_a_base64_code()
        {
            foreach (var name in Wanted)
            {
                Assert.True(IrccCodes.Fallback.ContainsKey(name), name);
                Assert.Equal(13, Convert.FromBase64String(IrccCodes.Fallback[name]).Length);
            }
        }

        [Fact]
        public void Fallback_matches_the_codes_the_tv_published_on_2026_10_28()
        {
            Assert.Equal("AAAAAQAAAAEAAAAJAw==", IrccCodes.Fallback["Num0"]);
            Assert.Equal("AAAAAQAAAAEAAAAAAw==", IrccCodes.Fallback["Num1"]);
            Assert.Equal("AAAAAQAAAAEAAAALAw==", IrccCodes.Fallback["Enter"]);
            Assert.Equal("AAAAAgAAAJcAAAAlAw==", IrccCodes.Fallback["Red"]);
            Assert.Equal("AAAAAgAAAJcAAAAmAw==", IrccCodes.Fallback["Green"]);
            Assert.Equal("AAAAAgAAAJcAAAAnAw==", IrccCodes.Fallback["Yellow"]);
            Assert.Equal("AAAAAgAAAJcAAAAkAw==", IrccCodes.Fallback["Blue"]);
            Assert.Equal("AAAAAgAAAJcAAAAaAw==", IrccCodes.Fallback["Play"]);
            Assert.Equal("AAAAAgAAAJcAAAAZAw==", IrccCodes.Fallback["Pause"]);
            Assert.Equal("AAAAAgAAAKQAAABbAw==", IrccCodes.Fallback["EPG"]);
            Assert.Equal("AAAAAQAAAAEAAAAOAw==", IrccCodes.Fallback["GGuide"]);
            Assert.Equal("AAAAAQAAAAEAAABjAw==", IrccCodes.Fallback["Exit"]);
            Assert.Equal("AAAAAQAAAAEAAAA+Aw==", IrccCodes.Fallback["PicOff"]);
            Assert.Equal("AAAAAgAAABoAAABYAw==", IrccCodes.Fallback["SyncMenu"]);
        }

        [Fact]
        public void Fallback_leaves_out_the_japanese_keys_and_the_dead_internet_services()
        {
            foreach (var name in Excluded) Assert.False(IrccCodes.Fallback.ContainsKey(name), name);
        }

        [Fact]
        public void Resolve_prefers_the_tv_code_and_falls_back_to_the_table()
        {
            var tv = new Dictionary<string, string> { ["Play"] = "FROM-TV" };
            Assert.Equal("FROM-TV", IrccCodes.Resolve(tv, "Play"));
            Assert.Equal(IrccCodes.Fallback["Pause"], IrccCodes.Resolve(tv, "Pause"));
            Assert.Equal(IrccCodes.Fallback["Pause"], IrccCodes.Resolve(null, "Pause"));
        }

        [Fact]
        public void Guide_uses_EPG_then_GGuide_then_the_table()
        {
            var both = new Dictionary<string, string> { ["EPG"] = "E", ["GGuide"] = "G" };
            var onlyG = new Dictionary<string, string> { ["GGuide"] = "G" };
            var none = new Dictionary<string, string> { ["Play"] = "P" };
            Assert.Equal("E", IrccCodes.ResolveGuide(both));
            Assert.Equal("G", IrccCodes.ResolveGuide(onlyG));
            Assert.Equal(IrccCodes.Fallback["EPG"], IrccCodes.ResolveGuide(none));
            Assert.Equal(IrccCodes.Fallback["EPG"], IrccCodes.ResolveGuide(null));
        }
    }

    public class OutcomeTests
    {
        [Theory]
        [InlineData(0, null, ScalarOutcome.NoResponse)]
        [InlineData(200, "{\"id\":1,\"result\":[]}", ScalarOutcome.Ok)]
        [InlineData(403, "{\"auth_url\":{}}", ScalarOutcome.NotPaired)]
        [InlineData(200, "{\"error\":[403,\"Forbidden\"],\"id\":1}", ScalarOutcome.NotPaired)]
        [InlineData(200, "{\"error\":[12,\"No such method\"],\"id\":1}", ScalarOutcome.NotSupported)]
        [InlineData(200, "{\"error\":[14,\"Unsupported version\"],\"id\":1}", ScalarOutcome.NotSupported)]
        [InlineData(200, "{\"error\":[15,\"Not supported\"],\"id\":1}", ScalarOutcome.NotSupported)]
        [InlineData(404, "", ScalarOutcome.NotSupported)]
        [InlineData(200, "{\"error\":[7,\"Clock is not set\"],\"id\":1}", ScalarOutcome.TvError)]
        [InlineData(200, "{\"error\":[3,\"Illegal argument\"],\"id\":1}", ScalarOutcome.TvError)]
        [InlineData(500, "oops", ScalarOutcome.HttpFailed)]
        public void Classify_maps_the_reply_to_one_outcome(int status, string body, ScalarOutcome expected)
            => Assert.Equal(expected, ScalarReply.Classify(status, body));

        [Fact]
        public void Not_supported_wins_over_a_generic_tv_error_but_not_over_pairing()
        {
            Assert.True(ScalarReply.IsUnsupported(200, "{\"error\":[15,\"x\"],\"id\":1}"));
            Assert.False(ScalarReply.IsUnsupported(200, "{\"error\":[7,\"x\"],\"id\":1}"));
            Assert.Equal(ScalarOutcome.NotPaired, ScalarReply.Classify(403, "{\"error\":[12,\"x\"],\"auth_url\":{}}"));
        }

        [Theory]
        [InlineData("{\"id\":1,\"result\":[[{\"name\":\"textInput\",\"status\":\"off\"},{\"name\":\"webBrowse\",\"status\":\"off\"}]]}", "textInput", "off")]
        [InlineData("{\"id\":1,\"result\":[[{\"name\":\"textInput\",\"status\":\"on\"}]]}", "textInput", "on")]
        [InlineData("{\"id\":1,\"result\":[[{\"name\":\"webBrowse\",\"status\":\"on\"}]]}", "textInput", null)]
        [InlineData("garbage", "textInput", null)]
        [InlineData(null, "textInput", null)]
        [InlineData("{\"error\":[12,\"x\"],\"id\":1}", "textInput", null)]
        public void AppStatus_reads_one_named_status(string body, string name, string expected)
            => Assert.Equal(expected, ScalarReply.AppStatus(body, name));

        [Theory]
        [InlineData("{\"id\":1,\"result\":[\"2026-10-28T09:42:31-0300\"]}", "2026-10-28T09:42:31-0300")]
        [InlineData("{\"error\":[7,\"Clock is not set\"],\"id\":1}", null)]
        [InlineData("garbage", null)]
        public void CurrentTime_reads_the_clock_string(string body, string expected)
            => Assert.Equal(expected, ScalarReply.CurrentTime(body));
    }

    public class FeatureClientTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "SonyTvRemoteTests-" + Guid.NewGuid());
        private const string Ip = "192.168.1.100";
        private readonly StubHandler _stub = new StubHandler();
        private readonly TvClient _tv;

        public FeatureClientTests()
        {
            var path = Path.Combine(_dir, "settings.json");
            var settings = Settings.Load(path);
            settings.SetCookie(Ip, "auth=abc", DateTime.UtcNow.AddDays(10));
            _tv = new TvClient(settings, path, _stub) { Ip = Ip };
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private (string Url, JsonElement Root) Sent()
        {
            var (req, body) = _stub.Requests.Single();
            return (req.RequestUri.ToString(), JsonDocument.Parse(body).RootElement);
        }

        [Fact]
        public async Task SetVolume_sends_the_speaker_target_and_the_volume_as_a_string()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[0]}");
            await _tv.SetVolume(17);
            var (url, root) = Sent();
            Assert.Equal("http://192.168.1.100/sony/audio", url);
            Assert.Equal("setAudioVolume", root.GetProperty("method").GetString());
            Assert.Equal("{\"target\":\"speaker\",\"volume\":\"17\"}", root.GetProperty("params")[0].GetRawText());
        }

        [Fact]
        public async Task GetVolume_asks_the_audio_service()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[[]]}");
            await _tv.GetVolume();
            var (url, root) = Sent();
            Assert.Equal("http://192.168.1.100/sony/audio", url);
            Assert.Equal("getVolumeInformation", root.GetProperty("method").GetString());
            Assert.Equal(0, root.GetProperty("params").GetArrayLength());
        }

        [Fact]
        public async Task GetInputs_asks_avContent_for_the_external_inputs_status()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[[]]}");
            await _tv.GetInputs();
            var (url, root) = Sent();
            Assert.Equal("http://192.168.1.100/sony/avContent", url);
            Assert.Equal("getCurrentExternalInputsStatus", root.GetProperty("method").GetString());
        }

        [Fact]
        public async Task PlayInput_sends_the_uri_with_setPlayContent()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[]}");
            await _tv.PlayInput("extInput:hdmi?port=1");
            var (url, root) = Sent();
            Assert.Equal("http://192.168.1.100/sony/avContent", url);
            Assert.Equal("setPlayContent", root.GetProperty("method").GetString());
            Assert.Equal("{\"uri\":\"extInput:hdmi?port=1\"}", root.GetProperty("params")[0].GetRawText());
        }

        [Fact]
        public async Task SendText_passes_the_text_itself_as_the_only_param()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[0]}");
            await _tv.SendText("olá \"mundo\"");
            var (url, root) = Sent();
            Assert.Equal("http://192.168.1.100/sony/appControl", url);
            Assert.Equal("setTextForm", root.GetProperty("method").GetString());
            Assert.Equal(1, root.GetProperty("params").GetArrayLength());
            Assert.Equal("olá \"mundo\"", root.GetProperty("params")[0].GetString());
        }

        [Fact]
        public async Task GetAppStatus_asks_appControl()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[[]]}");
            await _tv.GetAppStatus();
            var (url, root) = Sent();
            Assert.Equal("http://192.168.1.100/sony/appControl", url);
            Assert.Equal("getApplicationStatusList", root.GetProperty("method").GetString());
        }

        [Fact]
        public async Task SetClock_sends_the_utc_time_and_offsets()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[]}");
            var now = new DateTimeOffset(2026, 10, 28, 9, 42, 21, TimeSpan.FromHours(-3));
            await _tv.SetClock(now, TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(-3), "t", "t"));
            var (url, root) = Sent();
            Assert.Equal("http://192.168.1.100/sony/system", url);
            Assert.Equal("setCurrentTime", root.GetProperty("method").GetString());
            Assert.Equal("{\"dateTime\":\"2026-10-28T12:42:21Z\",\"timeZoneOffsetMinute\":-180,\"dstOffsetMinute\":0}",
                root.GetProperty("params")[0].GetRawText());
        }

        [Fact]
        public async Task GetColorLayout_and_GetCurrentTime_ask_the_system_service()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[{}]}");
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[\"x\"]}");
            await _tv.GetColorLayout();
            await _tv.GetCurrentTime();
            Assert.Equal("getColorKeysLayout", JsonDocument.Parse(_stub.Requests[0].Body).RootElement.GetProperty("method").GetString());
            Assert.Equal("getCurrentTime", JsonDocument.Parse(_stub.Requests[1].Body).RootElement.GetProperty("method").GetString());
            Assert.All(_stub.Requests, r => Assert.Equal("http://192.168.1.100/sony/system", r.Req.RequestUri.ToString()));
        }
    }

    public class TextInputTests : IDisposable
    {
        private readonly string _dir = Path.Combine(Path.GetTempPath(), "SonyTvRemoteTests-" + Guid.NewGuid());
        private const string Ip = "192.168.1.100";
        private readonly StubHandler _stub = new StubHandler();
        private readonly Settings _settings;
        private readonly TvClient _tv;

        public TextInputTests()
        {
            var path = Path.Combine(_dir, "settings.json");
            _settings = Settings.Load(path);
            _settings.SetCookie(Ip, "auth=abc", DateTime.UtcNow.AddDays(10));
            _tv = new TvClient(_settings, path, _stub) { Ip = Ip };
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private static string Status(string textInput)
            => "{\"id\":1,\"result\":[[{\"name\":\"textInput\",\"status\":\"" + textInput + "\"},{\"name\":\"webBrowse\",\"status\":\"off\"}]]}";

        private string[] Methods()
            => _stub.Requests.Select(r => JsonDocument.Parse(r.Body).RootElement.GetProperty("method").GetString()).ToArray();

        [Fact]
        public async Task Field_off_says_so_and_never_calls_setTextForm()
        {
            _stub.Reply(HttpStatusCode.OK, Status("off"));
            var result = await TextForm.Send(_tv, "hello");
            Assert.Equal(TextInputKind.FieldOff, result.Kind);
            Assert.Equal(new[] { "getApplicationStatusList" }, Methods());
        }

        [Fact]
        public async Task Field_on_sends_the_text_with_setTextForm()
        {
            _stub.Reply(HttpStatusCode.OK, Status("on"));
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[0]}");
            var result = await TextForm.Send(_tv, "hello");
            Assert.Equal(TextInputKind.Called, result.Kind);
            Assert.True(result.Reply.Ok);
            Assert.Equal(new[] { "getApplicationStatusList", "setTextForm" }, Methods());
            Assert.Equal("hello", JsonDocument.Parse(_stub.Requests[1].Body).RootElement.GetProperty("params")[0].GetString());
        }

        [Fact]
        public async Task Field_on_but_not_paired_does_not_call_setTextForm()
        {
            _settings.ClearCookie(Ip);
            _stub.Reply(HttpStatusCode.OK, Status("on"));
            var result = await TextForm.Send(_tv, "hello");
            Assert.Equal(TextInputKind.NotPaired, result.Kind);
            Assert.Equal(new[] { "getApplicationStatusList" }, Methods());
        }

        [Fact]
        public async Task A_tv_without_the_method_is_not_supported()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"error\":[12,\"No such method\"],\"id\":1}");
            var result = await TextForm.Send(_tv, "hello");
            Assert.Equal(TextInputKind.NotSupported, result.Kind);
            Assert.Single(_stub.Requests);
        }

        [Fact]
        public async Task A_reply_with_no_textInput_entry_is_not_supported()
        {
            _stub.Reply(HttpStatusCode.OK, "{\"id\":1,\"result\":[[{\"name\":\"webBrowse\",\"status\":\"on\"}]]}");
            var result = await TextForm.Send(_tv, "hello");
            Assert.Equal(TextInputKind.NotSupported, result.Kind);
            Assert.Single(_stub.Requests);
        }

        [Fact]
        public async Task No_answer_from_the_tv_is_a_failure_with_the_reply_to_report()
        {
            // No reply queued: the stub throws, which the client reports as Status 0.
            var result = await TextForm.Send(_tv, "hello");
            Assert.Equal(TextInputKind.Failed, result.Kind);
            Assert.Equal(0, result.Reply.Status);
            Assert.Single(_stub.Requests);
        }

        [Fact]
        public async Task A_tv_error_on_setTextForm_comes_back_as_the_reply_for_the_caller_to_show()
        {
            _stub.Reply(HttpStatusCode.OK, Status("on"));
            _stub.Reply(HttpStatusCode.OK, "{\"error\":[3,\"Illegal argument\"],\"id\":1}");
            var result = await TextForm.Send(_tv, "hello");
            Assert.Equal(TextInputKind.Called, result.Kind);
            Assert.False(result.Reply.Ok);
            Assert.Equal(ScalarOutcome.TvError, ScalarReply.Classify(result.Reply.Status, result.Reply.Body));
        }
    }

    public class NewStringsTests
    {
        public static readonly string[] Keys =
        {
            "TabRemote", "TabNumbers", "TabMedia", "TabInputs", "TabMore",
            "Enter", "Red", "Green", "Yellow", "Blue",
            "Play", "Pause", "Stop", "Rewind", "Forward", "Prev", "Next",
            "Guide", "Subtitle", "Audio", "Wide", "Exit", "PicOff", "SyncMenu",
            "Refresh", "InputsEmpty", "InputSwitched", "InputConnected",
            "VolumeExact", "VolumeSet",
            "TextHint", "SendText", "TextFieldOff", "TextSent",
            "SyncClock", "ClockSet", "ClockSetRead",
            "NotSupported",
        };

        [Fact]
        public void Every_new_key_exists_in_both_languages()
        {
            foreach (var lang in new[] { Lang.EnUs, Lang.PtBr })
                foreach (var key in Keys)
                    Assert.True(Strings.Table(lang).ContainsKey(key), lang + ":" + key);
        }

        [Fact]
        public void Required_messages_use_the_wording_from_the_spec()
        {
            Assert.Equal("Not supported by this TV", Strings.Get(Lang.EnUs, "NotSupported"));
            Assert.Equal("Não suportado por esta TV", Strings.Get(Lang.PtBr, "NotSupported"));
            Assert.Equal("Open a text field on the TV first", Strings.Get(Lang.EnUs, "TextFieldOff"));
            Assert.Equal("Abra um campo de texto na TV primeiro", Strings.Get(Lang.PtBr, "TextFieldOff"));
            Assert.Equal("Send text", Strings.Get(Lang.EnUs, "SendText"));
            Assert.Equal("Enviar texto", Strings.Get(Lang.PtBr, "SendText"));
            Assert.Equal("Sync TV clock", Strings.Get(Lang.EnUs, "SyncClock"));
            Assert.Equal("Acertar relógio da TV", Strings.Get(Lang.PtBr, "SyncClock"));
            Assert.Equal("Refresh", Strings.Get(Lang.EnUs, "Refresh"));
            Assert.Equal("Atualizar", Strings.Get(Lang.PtBr, "Refresh"));
            Assert.Equal("Numbers & colors", Strings.Get(Lang.EnUs, "TabNumbers"));
            Assert.Equal("Números e cores", Strings.Get(Lang.PtBr, "TabNumbers"));
            Assert.Equal("Controle", Strings.Get(Lang.PtBr, "TabRemote"));
            Assert.Equal("Mídia", Strings.Get(Lang.PtBr, "TabMedia"));
            Assert.Equal("Entradas", Strings.Get(Lang.PtBr, "TabInputs"));
            Assert.Equal("Mais", Strings.Get(Lang.PtBr, "TabMore"));
        }

        [Fact]
        public void Changelog_describes_1_3_0_in_both_languages()
        {
            foreach (var lang in new[] { Lang.EnUs, Lang.PtBr })
            {
                var text = Strings.Get(lang, "ChangelogText");
                Assert.StartsWith("1.3.0 ", text);
                Assert.Contains("1.2.0", text);
            }
        }
    }
}
