using System;
using System.Collections.Generic;
using System.Linq;
using SonyTvRemote;
using Xunit;

namespace SonyTvRemote.Tests
{
    public class AllKeysTests
    {
        [Fact]
        public void Drops_the_excluded_keys_ignoring_case()
        {
            var list = AllKeys.Build(new[] { "Home", "bs", "CS", "BSCS", "ddata", "ACTVILA", "RakurakuStart",
                "netflix", "InternetVideo", "InternetWidgets", "DeleteVideo", "Analog" });
            Assert.Equal(new[] { "Analog", "Home" }, list);
        }

        [Fact]
        public void Sorts_without_regard_to_case()
        {
            var list = AllKeys.Build(new[] { "volumeUp", "Audio", "mute", "Num1", "Home", "audio2" });
            Assert.Equal(new[] { "Audio", "audio2", "Home", "mute", "Num1", "volumeUp" }, list);
        }

        [Fact]
        public void Without_the_tv_list_it_uses_the_fallback_table()
        {
            var list = AllKeys.Build(null);
            Assert.Contains("Analog", list);
            Assert.Contains("Home", list);
            Assert.Equal(IrccCodes.Fallback.Count, list.Count);
            Assert.True(list.SequenceEqual(list.OrderBy(n => n, StringComparer.OrdinalIgnoreCase)));
        }

        [Fact]
        public void Skips_empty_names_and_duplicates()
        {
            Assert.Equal(new[] { "Home" }, AllKeys.Build(new[] { "Home", "", null, "home" }));
        }
    }

    public class VolumeThrottleTests
    {
        private static TimeSpan Ms(int ms) => TimeSpan.FromMilliseconds(ms);

        [Fact]
        public void First_move_goes_out_at_once()
        {
            var t = new VolumeThrottle(10);
            Assert.Equal(11, t.Offer(11, Ms(0)));
        }

        [Fact]
        public void At_most_one_send_every_250_ms_and_the_latest_value_wins()
        {
            var t = new VolumeThrottle(10);
            Assert.Equal(11, t.Offer(11, Ms(0)));
            Assert.Null(t.Offer(12, Ms(50)));
            Assert.Null(t.Offer(13, Ms(100)));
            Assert.True(t.HasPending);
            Assert.Null(t.Take(Ms(249)));
            Assert.Equal(13, t.Take(Ms(250)));
            Assert.False(t.HasPending);
            Assert.Null(t.Take(Ms(600)));
        }

        [Fact]
        public void Interval_counts_from_the_last_send()
        {
            var t = new VolumeThrottle(0);
            Assert.Equal(1, t.Offer(1, Ms(0)));
            Assert.Equal(2, t.Offer(2, Ms(250)));
            Assert.Null(t.Offer(3, Ms(499)));
            Assert.Equal(3, t.Take(Ms(500)));
        }

        [Fact]
        public void Release_sends_the_final_value_even_inside_the_interval()
        {
            var t = new VolumeThrottle(10);
            Assert.Equal(11, t.Offer(11, Ms(0)));
            Assert.Null(t.Offer(15, Ms(10)));
            Assert.Equal(15, t.Release(15, Ms(20)));
            Assert.False(t.HasPending);
        }

        [Fact]
        public void Release_sends_nothing_when_the_value_is_the_last_one_sent()
        {
            var t = new VolumeThrottle(10);
            Assert.Equal(12, t.Offer(12, Ms(0)));
            Assert.Null(t.Release(12, Ms(500)));
        }

        [Fact]
        public void Release_back_on_the_starting_value_sends_nothing()
        {
            var t = new VolumeThrottle(10);
            Assert.Null(t.Release(10, Ms(0)));
        }

        [Fact]
        public void A_waiting_value_equal_to_the_last_sent_is_not_sent()
        {
            var t = new VolumeThrottle(10);
            Assert.Equal(11, t.Offer(11, Ms(0)));
            Assert.Null(t.Offer(12, Ms(10)));
            Assert.Null(t.Offer(11, Ms(20)));
            Assert.Null(t.Take(Ms(300)));
            Assert.False(t.HasPending);
        }

        [Fact]
        public void Reset_takes_the_tv_value_as_the_reference()
        {
            var t = new VolumeThrottle(10);
            t.Offer(11, Ms(0));
            t.Offer(12, Ms(10));
            t.Reset(30);
            Assert.False(t.HasPending);
            Assert.Null(t.Release(30, Ms(20)));
            Assert.Equal(31, t.Offer(31, Ms(30)));
        }
    }

    public class AutoReadTests
    {
        [Fact]
        public void Volume_read_is_skipped_while_the_slider_is_held()
            => Assert.False(AutoRead.VolumeAllowed(true, null, false));

        [Fact]
        public void Volume_read_is_skipped_within_2_seconds_of_a_send()
        {
            Assert.False(AutoRead.VolumeAllowed(false, TimeSpan.FromMilliseconds(1999), false));
            Assert.True(AutoRead.VolumeAllowed(false, TimeSpan.FromSeconds(2), false));
        }

        [Fact]
        public void Volume_read_is_skipped_while_another_read_runs()
            => Assert.False(AutoRead.VolumeAllowed(false, null, true));

        [Fact]
        public void Volume_read_runs_when_nothing_is_in_the_way()
            => Assert.True(AutoRead.VolumeAllowed(false, null, false));

        [Fact]
        public void Inputs_read_is_skipped_while_the_previous_one_runs()
        {
            Assert.False(AutoRead.InputsAllowed(true));
            Assert.True(AutoRead.InputsAllowed(false));
        }
    }

    public class InputListCompareTests
    {
        private static List<TvInput> L(params (string t, string u, bool c)[] items)
            => items.Select(i => new TvInput { Title = i.t, Uri = i.u, Connected = i.c }).ToList();

        [Fact]
        public void Same_lists_are_the_same()
            => Assert.True(InputList.SameList(L(("HDMI 1", "a", true), ("HDMI 2", "b", false)), L(("HDMI 1", "a", true), ("HDMI 2", "b", false))));

        [Fact]
        public void A_connection_change_is_a_change()
            => Assert.False(InputList.SameList(L(("HDMI 1", "a", true)), L(("HDMI 1", "a", false))));

        [Fact]
        public void A_title_or_uri_change_is_a_change()
        {
            Assert.False(InputList.SameList(L(("HDMI 1", "a", true)), L(("Game", "a", true))));
            Assert.False(InputList.SameList(L(("HDMI 1", "a", true)), L(("HDMI 1", "b", true))));
        }

        [Fact]
        public void A_different_length_is_a_change()
            => Assert.False(InputList.SameList(L(("HDMI 1", "a", true)), L()));

        [Fact]
        public void Two_empty_lists_are_the_same()
            => Assert.True(InputList.SameList(L(), L()));
    }

    public class Release130Tests
    {
        [Fact]
        public void Number_pad_last_row_sends_Enter_and_Confirm_and_both_have_codes()
        {
            // The last row is Enter | 0 | OK: Enter sends "Enter", OK sends "Confirm" (labelled "OK").
            Assert.Equal("AAAAAQAAAAEAAAALAw==", IrccCodes.Resolve(null, "Enter"));
            Assert.Equal(IrccCodes.Fallback["Confirm"], IrccCodes.Resolve(null, "Confirm"));
            Assert.NotEqual(IrccCodes.Resolve(null, "Enter"), IrccCodes.Resolve(null, "Confirm"));
            Assert.Equal("Enter", Strings.Get(Lang.EnUs, "Enter"));
            Assert.Equal("OK", Strings.Get(Lang.EnUs, "Confirm"));
            Assert.Equal("OK", Strings.Get(Lang.PtBr, "Confirm"));
        }

        [Fact]
        public void Fallback_has_Analog_for_the_ANT_button()
            => Assert.Equal("AAAAAgAAAHcAAAANAw==", IrccCodes.Fallback["Analog"]);

        [Fact]
        public void The_tv_list_wins_over_the_fallback_for_Analog()
            => Assert.Equal("X", IrccCodes.Resolve(new Dictionary<string, string> { ["Analog"] = "X" }, "Analog"));

        [Theory]
        [InlineData("Ant")]
        [InlineData("AntTip")]
        [InlineData("TipReturn")]
        [InlineData("TipHome")]
        [InlineData("TipOptions")]
        [InlineData("TipDisplay")]
        [InlineData("AllKeysLabel")]
        [InlineData("SendKey")]
        [InlineData("ChooseKey")]
        public void New_strings_exist_in_both_languages(string key)
        {
            Assert.True(Strings.Table(Lang.EnUs).ContainsKey(key), key);
            Assert.True(Strings.Table(Lang.PtBr).ContainsKey(key), key);
            Assert.False(string.IsNullOrWhiteSpace(Strings.Get(Lang.EnUs, key)));
            Assert.False(string.IsNullOrWhiteSpace(Strings.Get(Lang.PtBr, key)));
        }

        [Fact]
        public void New_strings_have_the_agreed_texts()
        {
            Assert.Equal("ANT: switch Antenna / Cable", Strings.Get(Lang.EnUs, "AntTip"));
            Assert.Equal("ANT: alterna Antena / Cabo", Strings.Get(Lang.PtBr, "AntTip"));
            Assert.Equal("All TV keys:", Strings.Get(Lang.EnUs, "AllKeysLabel"));
            Assert.Equal("Todas as teclas da TV:", Strings.Get(Lang.PtBr, "AllKeysLabel"));
            Assert.Equal("Send", Strings.Get(Lang.EnUs, "SendKey"));
            Assert.Equal("Enviar", Strings.Get(Lang.PtBr, "SendKey"));
            Assert.Equal("Choose a key", Strings.Get(Lang.EnUs, "ChooseKey"));
            Assert.Equal("Escolha uma tecla", Strings.Get(Lang.PtBr, "ChooseKey"));
            foreach (var lang in new[] { Lang.EnUs, Lang.PtBr })
            {
                Assert.Equal("RETURN", Strings.Get(lang, "TipReturn"));
                Assert.Equal("HOME", Strings.Get(lang, "TipHome"));
                Assert.Equal("OPTIONS", Strings.Get(lang, "TipOptions"));
                Assert.Equal("DISPLAY", Strings.Get(lang, "TipDisplay"));
            }
        }

        [Fact]
        public void Version_is_1_5_0()
            => Assert.Equal("1.5.0", AppInfo.Version);
    }
}
