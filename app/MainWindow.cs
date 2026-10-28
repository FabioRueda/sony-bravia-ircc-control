using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonyTvRemote
{
    public class MainWindow : Window
    {
        private readonly Settings _settings;
        private readonly string _settingsPath = Settings.DefaultPath;
        private readonly TvClient _tv;

        // Re-applies every text when the language changes.
        private readonly List<Action> _relabel = new List<Action>();

        private readonly TextBox _ipBox;
        private readonly TextBox _urlBox;
        private readonly ComboBox _langBox;
        private readonly TextBlock _status;

        // Last status shown, kept so it can be re-rendered in the other language.
        private string _statusKey = "Ready";
        private object[] _statusArgs = new object[0];
        private bool _statusError;

        // The TV's own IRCC codes, fetched on first use (per IP) and kept in memory.
        // After a failed fetch, wait a minute before asking again.
        private static readonly TimeSpan InfoRetry = TimeSpan.FromMinutes(1);
        private Dictionary<string, string> _codes;
        private string _infoIp;
        private DateTime _infoFailedUtc = DateTime.MinValue;
        private bool _powerBusy;
        private bool _pairBusy;

        public MainWindow()
        {
            _settings = Settings.Load(_settingsPath);
            Strings.Current = Strings.FromCode(_settings.Language);
            _tv = new TvClient(_settings, _settingsPath);

            Width = 410;
            Height = 690;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new StackPanel { Margin = new Thickness(14), Spacing = 8 };

            // --- Language + About ---
            _langBox = new ComboBox { Width = 170 };
            _langBox.ItemsSource = new[] { Strings.Get("LangEnUs"), Strings.Get("LangPtBr") };
            _langBox.SelectedIndex = Strings.Current == Lang.PtBr ? 1 : 0;
            _langBox.SelectionChanged += (s, e) => ChangeLanguage(_langBox.SelectedIndex == 1 ? Lang.PtBr : Lang.EnUs);
            var aboutBtn = new Button { Width = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
            Label(aboutBtn, "About");
            aboutBtn.Click += async (s, e) => await Safe(() => new AboutWindow().ShowDialog(this));
            root.Children.Add(Row(Text("LanguageLabel"), _langBox, aboutBtn));

            // --- TV IP + pairing ---
            _ipBox = new TextBox { Text = _settings.Ip, Width = 150, Watermark = Settings.DefaultIp };
            var pairBtn = new Button { Width = 140, HorizontalContentAlignment = HorizontalAlignment.Center };
            Label(pairBtn, "Pair");
            pairBtn.Click += async (s, e) => await Safe(Pair);
            root.Children.Add(Row(Text("TvIp"), _ipBox, pairBtn));

            // --- Power ---
            root.Children.Add(Row(
                Btn("PowerOn", PowerOn),
                Key("PowerOff", "PowerOff"),
                Key("Mute", "Mute")));

            // --- Volume / Channel ---
            root.Children.Add(Row(
                Key("VolDown", "VolumeDown"),
                Key("VolUp", "VolumeUp"),
                Key("ChDown", "ChannelDown"),
                Key("ChUp", "ChannelUp")));

            // --- D-pad ---
            var grid = new Grid
            {
                Width = 190,
                Height = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                RowDefinitions = new RowDefinitions("*,*,*")
            };
            Place(grid, DBtn("▲", "Up"), 1, 0);
            Place(grid, DBtn("◄", "Left"), 0, 1);
            Place(grid, DBtn(null, "Confirm"), 1, 1);
            Place(grid, DBtn("►", "Right"), 2, 1);
            Place(grid, DBtn("▼", "Down"), 1, 2);
            root.Children.Add(grid);

            // --- Navigation ---
            root.Children.Add(Row(
                Key("Back", "Return"),
                Key("Home", "Home"),
                Key("Options", "Options"),
                Key("Display", "Display")));

            // --- Inputs (HDMI codes are not in the TV's list; standard BRAVIA codes) ---
            root.Children.Add(Row(
                Key("Input", "Input"),
                Btn("Hdmi1", () => Ircc(IrccCodes.Hdmi1)),
                Btn("Hdmi2", () => Ircc(IrccCodes.Hdmi2))));
            root.Children.Add(Row(
                Btn("Hdmi3", () => Ircc(IrccCodes.Hdmi3)),
                Btn("Hdmi4", () => Ircc(IrccCodes.Hdmi4))));

            // --- Open a page on the TV (needs pairing) ---
            _urlBox = new TextBox { Width = 230 };
            _relabel.Add(() => _urlBox.Watermark = Strings.Get("UrlHint"));
            var openBtn = new Button { Width = 130, HorizontalContentAlignment = HorizontalAlignment.Center };
            Label(openBtn, "OpenOnTv");
            openBtn.Click += async (s, e) => await Safe(OpenPage);
            root.Children.Add(Row(_urlBox, openBtn));

            // --- Status ---
            _status = new TextBlock
            {
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            root.Children.Add(_status);

            Content = root;
            Closing += (s, e) => SaveSettings();
            ApplyLanguage();
        }

        // ---------- language ----------

        private void ChangeLanguage(Lang lang)
        {
            if (lang == Strings.Current) return;
            Strings.Current = lang;
            _settings.Language = Strings.Code(lang);
            SaveSettings();
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            Title = Strings.Get("TitleWithVersion", Strings.Get("AppTitle"), AppInfo.Version);
            foreach (var relabel in _relabel) relabel();
            RenderStatus();
        }

        // ---------- UI helpers ----------

        private void Label(ContentControl c, string key) => _relabel.Add(() => c.Content = Strings.Get(key));

        private TextBlock Text(string key)
        {
            var t = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
            _relabel.Add(() => t.Text = Strings.Get(key));
            return t;
        }

        private Button Btn(string key, Func<Task> action)
        {
            var b = new Button { Width = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
            Label(b, key);
            b.Click += async (s, e) => await Safe(action);
            return b;
        }

        // A button that sends the IRCC code the TV publishes under irccName.
        private Button Key(string key, string irccName) => Btn(key, () => SendNamed(irccName));

        // D-pad button: arrow glyphs are symbols; the center one is labelled from the dictionary.
        private Button DBtn(string glyph, string irccName)
        {
            var b = new Button
            {
                Content = glyph,
                Margin = new Thickness(3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 15
            };
            if (glyph == null) Label(b, irccName);
            b.Click += async (s, e) => await Safe(() => SendNamed(irccName));
            return b;
        }

        // Click handlers are async void: an escaping exception would close the app.
        private async Task Safe(Func<Task> action)
        {
            try { await action(); }
            catch (Exception) { SetStatus("Unexpected", true); }
        }

        private static StackPanel Row(params Control[] items)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            foreach (var c in items) p.Children.Add(c);
            return p;
        }

        private static void Place(Grid g, Control c, int col, int row)
        {
            Grid.SetColumn(c, col);
            Grid.SetRow(c, row);
            g.Children.Add(c);
        }

        private void SetStatus(string key, bool error, params object[] args)
        {
            _statusKey = key;
            _statusArgs = args;
            _statusError = error;
            RenderStatus();
        }

        private void RenderStatus()
        {
            _status.Foreground = _statusKey == "Ready" ? Brushes.Gray
                : _statusError ? Brushes.Firebrick : Brushes.ForestGreen;
            _status.Text = Strings.Get(_statusKey, _statusArgs);
        }

        private void SaveSettings()
        {
            _settings.Ip = Ip();
            try { _settings.Save(_settingsPath); } catch (Exception) { }
        }

        private string Ip() => (_ipBox.Text ?? "").Trim();


        // Points the client at the typed IP. False (with a message) when it is empty or invalid.
        private bool UseIp()
        {
            var ip = Ip();
            if (ip.Length == 0) { SetStatus("EnterIp", true); return false; }
            if (!UrlHelper.IsValidHost(ip)) { SetStatus("InvalidIp", true); return false; }
            _tv.Ip = ip;
            SaveSettings();
            return true;
        }

        // ---------- actions ----------

        // First contact with a TV: its IRCC code list and its Wake-on-LAN MAC (saved for standby).
        // Quick timeouts, and after a failure no new attempt for a minute.
        private async Task LoadTvInfo()
        {
            if (_infoIp != _tv.Ip)
            {
                _infoIp = _tv.Ip;
                _codes = null;
                _infoFailedUtc = DateTime.MinValue;
            }
            bool needCodes = _codes == null, needMac = _settings.Mac == null;
            if (!needCodes && !needMac) return;
            if (DateTime.UtcNow - _infoFailedUtc < InfoRetry) return;

            if (needCodes)
            {
                var r = await _tv.QuickScalar("system", "getRemoteControllerInfo");
                if (r.Status == 200) _codes = IrccCodes.Parse(r.Body);
                if (_codes == null) _infoFailedUtc = DateTime.UtcNow;
                if (r.Status == 0) return;
            }
            if (needMac && !await SaveMac()) _infoFailedUtc = DateTime.UtcNow;
        }

        private async Task<bool> SaveMac()
        {
            var mac = ScalarReply.WolMac((await _tv.QuickScalar("system", "getSystemSupportedFunction")).Body);
            if (mac == null) return false;
            if (mac != _settings.Mac)
            {
                _settings.Mac = mac;
                SaveSettings();
            }
            return true;
        }

        private async Task SendNamed(string irccName)
        {
            if (!UseIp()) return;
            await LoadTvInfo();
            await Ircc(IrccCodes.Resolve(_codes, irccName));
        }

        private async Task Ircc(string code)
        {
            if (!UseIp()) return;
            var r = await _tv.Ircc(code);
            if (r.Status == 0) SetStatus("NoResponse", true);
            else if (r.Status == 200) SetStatus("SentOk", false, r.Status);
            else SetStatus("HttpFailed", true, r.Status);
        }

        private async Task Pair()
        {
            if (_pairBusy) { SetStatus("Busy", false); return; }
            if (!UseIp()) return;
            _pairBusy = true;
            try
            {
                SetStatus("PairAsking", false);

                var first = await _tv.ActRegister(null, false);
                if (first.Status == 0) { SetStatus("NoResponse", true); return; }

                // Already registered: the TV answers 200 with a cookie right away.
                if (!_tv.SaveCookie(first))
                {
                    if (first.Status != 401) { SetStatus("PairFailed", true, first.Status); return; }

                    var pin = await new PinWindow().ShowDialog<string>(this);
                    if (pin == null) { SetStatus("PairCancelled", true); return; }

                    var second = await _tv.ActRegister(pin, false);
                    if (second.Status == 0) { SetStatus("NoResponse", true); return; }
                    if (!_tv.SaveCookie(second)) { SetStatus("PairFailed", true, second.Status); return; }
                }

                await SaveMac();
                SetStatus("Paired", false, _tv.PairedUntilUtc.Value.ToLocalTime());
            }
            finally
            {
                _pairBusy = false;
            }
        }

        private async Task PowerOn()
        {
            if (_powerBusy) { SetStatus("Busy", false); return; }
            if (!UseIp()) return;
            _powerBusy = true;
            try
            {
                SetStatus("PowerWaking", false);

                // Wake-on-LAN goes out first with the saved MAC; only without one do we ask the TV.
                var mac = _settings.Mac;
                if (mac == null)
                {
                    await LoadTvInfo();
                    mac = _settings.Mac;
                }

                var clock = Stopwatch.StartNew();
                bool on = await PowerOnSequence.RunAsync(
                    sendWol: () => mac != null ? WakeOnLan.SendAsync(mac) : Task.CompletedTask,
                    isActive: async () => ScalarReply.PowerStatus((await _tv.QuickScalar("system", "getPowerStatus")).Body) == "active",
                    // Only when paired. Error 15 "power on not supported" (KDL-42W805A) is ignored on purpose.
                    setPowerStatus: async () =>
                    {
                        await _tv.RenewIfNeeded();
                        if (_tv.IsPaired)
                            await _tv.QuickScalar("system", "setPowerStatus", new Dictionary<string, bool> { ["status"] = true });
                    },
                    irccPowerOn: async () => { await _tv.Ircc(IrccCodes.PowerOn); },
                    delay: Task.Delay,
                    elapsed: () => clock.Elapsed);

                if (on) SetStatus("PowerOnOk", false);
                else SetStatus(mac == null ? "PowerNoMac" : "PowerStillStandby", true);
            }
            finally
            {
                _powerBusy = false;
            }
        }

        private async Task OpenPage()
        {
            if (!UseIp()) return;
            var url = UrlHelper.Normalize(_urlBox.Text);
            if (url == null) { SetStatus("EnterUrl", true); return; }
            _urlBox.Text = url;

            await _tv.RenewIfNeeded();
            if (!_tv.IsPaired) { SetStatus("NotPaired", true); return; }

            var r = await _tv.Scalar("appControl", "setActiveApp",
                new Dictionary<string, string> { ["uri"] = "localapp://webappruntime?url=" + url });
            if (r.Status == 0) SetStatus("NoResponse", true);
            else if (ScalarReply.NeedsAuth(r.Status, r.Body)) SetStatus("NotPaired", true);
            else if (r.Ok) SetStatus("PageSent", false);
            else if (ScalarReply.ErrorCode(r.Body) is int code) SetStatus("TvError", true, code);
            else SetStatus("HttpFailed", true, r.Status);
        }
    }
}