using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

// UI-free logic for the 1.2.0 features: inputs list, exact volume, color keys, TV clock.
// Covered by tests/TvRemote.Tests/FeaturesTests.cs.

namespace SonyTvRemote
{
    public class TvInput
    {
        public string Title;
        public string Uri;
        public bool Connected;
    }

    public static class InputList
    {
        // avContent/getCurrentExternalInputsStatus: one entry per input, with the title the TV shows
        // and whether something is plugged in. Entries with no uri can't be selected and are skipped.
        public static List<TvInput> Parse(string body)
        {
            var list = new List<TvInput>();
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (!doc.RootElement.TryGetProperty("result", out var r) || r.GetArrayLength() == 0) return list;
                    foreach (var item in r[0].EnumerateArray())
                    {
                        var uri = item.TryGetProperty("uri", out var u) && u.ValueKind == JsonValueKind.String ? u.GetString() : null;
                        if (string.IsNullOrEmpty(uri)) continue;
                        var title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                        var connected = item.TryGetProperty("connection", out var c) && c.ValueKind == JsonValueKind.True;
                        list.Add(new TvInput { Title = string.IsNullOrWhiteSpace(title) ? uri : title, Uri = uri, Connected = connected });
                    }
                }
            }
            catch (Exception) { list.Clear(); }
            return list;
        }

        // Without pairing only the IRCC HDMI1-4 codes can switch input. Null for anything else.
        public static string IrccFallback(string uri)
        {
            switch (uri)
            {
                case "extInput:hdmi?port=1": return IrccCodes.Hdmi1;
                case "extInput:hdmi?port=2": return IrccCodes.Hdmi2;
                case "extInput:hdmi?port=3": return IrccCodes.Hdmi3;
                case "extInput:hdmi?port=4": return IrccCodes.Hdmi4;
                default: return null;
            }
        }
    }

    public class VolumeInfo
    {
        public int Volume;
        public int Min;
        public int Max;
        public bool Mute;

        // audio/getVolumeInformation lists one entry per target. The speaker is the one we control.
        public static VolumeInfo Parse(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (!doc.RootElement.TryGetProperty("result", out var r) || r.GetArrayLength() == 0) return null;
                    JsonElement? chosen = null;
                    foreach (var item in r[0].EnumerateArray())
                    {
                        if (chosen == null) chosen = item.Clone();
                        if (item.TryGetProperty("target", out var t) && t.GetString() == "speaker") { chosen = item.Clone(); break; }
                    }
                    if (chosen == null) return null;
                    var e = chosen.Value;
                    if (!e.TryGetProperty("volume", out var v) || !e.TryGetProperty("maxVolume", out var max)) return null;
                    return new VolumeInfo
                    {
                        Volume = v.GetInt32(),
                        Max = max.GetInt32(),
                        Min = e.TryGetProperty("minVolume", out var min) ? min.GetInt32() : 0,
                        Mute = e.TryGetProperty("mute", out var m) && m.ValueKind == JsonValueKind.True
                    };
                }
            }
            catch (Exception) { return null; }
        }

        public static int Clamp(int value, int max) => Math.Max(0, Math.Min(value, max));
    }

    public static class ColorKeys
    {
        public const string DefaultLayout = "RGYB";

        // system/getColorKeysLayout -> "RGYB"; null if the reply is unusable.
        public static string ParseLayout(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    if (doc.RootElement.TryGetProperty("result", out var r) && r.GetArrayLength() > 0
                        && r[0].TryGetProperty("colorKeysLayout", out var l) && l.ValueKind == JsonValueKind.String)
                        return l.GetString();
                }
            }
            catch (Exception) { }
            return null;
        }

        // IRCC names in the order the TV lays the keys out. Anything but four different
        // letters from R, G, Y, B gives the default order.
        public static string[] Order(string layout)
        {
            var letters = (layout ?? "").ToUpperInvariant();
            if (letters.Length != 4 || letters.Distinct().Count() != 4 || letters.Any(c => "RGYB".IndexOf(c) < 0))
                letters = DefaultLayout;
            return letters.Select(c => c == 'R' ? "Red" : c == 'G' ? "Green" : c == 'Y' ? "Yellow" : "Blue").ToArray();
        }
    }

    public enum TextInputKind { Called, FieldOff, NotSupported, NotPaired, Failed }

    public class TextInputResult
    {
        public TextInputKind Kind;
        public TvReply Reply;   // setTextForm's reply for Called; the failing reply for Failed
    }

    public static class TextForm
    {
        // Asks the TV whether a text field is open (appControl/getApplicationStatusList, no pairing)
        // and only then sends the text. setTextForm is never called with the field closed, or unpaired.
        public static async Task<TextInputResult> Send(TvClient tv, string text)
        {
            var status = await tv.GetAppStatus();
            switch (ScalarReply.Classify(status.Status, status.Body))
            {
                case ScalarOutcome.Ok: break;
                case ScalarOutcome.NotSupported: return new TextInputResult { Kind = TextInputKind.NotSupported, Reply = status };
                default: return new TextInputResult { Kind = TextInputKind.Failed, Reply = status };
            }

            var textInput = ScalarReply.AppStatus(status.Body, "textInput");
            if (textInput == null) return new TextInputResult { Kind = TextInputKind.NotSupported, Reply = status };
            if (textInput != "on") return new TextInputResult { Kind = TextInputKind.FieldOff, Reply = status };

            await tv.RenewIfNeeded();
            if (!tv.IsPaired) return new TextInputResult { Kind = TextInputKind.NotPaired, Reply = status };
            return new TextInputResult { Kind = TextInputKind.Called, Reply = await tv.SendText(text) };
        }
    }

    public static class TvClock
    {
        // Params for system/setCurrentTime. On the KDL-42W805A (2026-10-28) dateTime must be UTC with a
        // "Z" suffix: local times with an offset ("-03:00", "-0300") or none answer error 3 "Illegal
        // argument". The TV applies its own time zone; the offsets below are what the PC reports.
        public static Dictionary<string, object> Params(DateTimeOffset now, TimeZoneInfo zone)
        {
            var standard = zone.BaseUtcOffset;
            var dst = zone.IsDaylightSavingTime(now) ? now.Offset - standard : TimeSpan.Zero;
            return new Dictionary<string, object>
            {
                ["dateTime"] = now.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                ["timeZoneOffsetMinute"] = (int)standard.TotalMinutes,
                ["dstOffsetMinute"] = (int)dst.TotalMinutes
            };
        }
    }
}
