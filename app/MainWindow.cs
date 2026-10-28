using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Shapes;
using Avalonia.Styling;
using Avalonia.Threading;

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

        private TabControl _tabs;

        // Numbers & colors: the color keys follow the TV's own layout (RGYB by default).
        private StackPanel _colorRow;
        private string _colorLayout = ColorKeys.DefaultLayout;
        private string _colorIp;

        // Inputs: the list the TV reported, rendered again when the language changes.
        private StackPanel _inputsPanel;
        private List<TvInput> _inputs = new List<TvInput>();
        private string _inputsIp;

        // More: exact volume. Set only when the slider is released.
        private Slider _volSlider;
        private TextBlock _volNumber;
        private TextBlock _volNote;
        private int _volApplied = -1;
        private bool _volReadOnly;
        private bool _volSending;
        private TextBox _textBox;

        // More: the slider sends while it moves (at most every 250 ms) and follows the remote control.
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly VolumeThrottle _volThrottle = new VolumeThrottle(-1);
        private bool _volHeld;            // mouse or key is holding the slider
        private bool _volFromTv;          // the slider is being set from a TV reading: never send
        private bool _volReleaseQueued;   // released while a send was running
        private bool _volReading;
        private TimeSpan? _volLastSendAt;
        private bool _inputsReading;
        private readonly DispatcherTimer _inputsTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        private readonly DispatcherTimer _volReadTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private readonly DispatcherTimer _volPumpTimer = new DispatcherTimer { Interval = VolumeThrottle.Interval };

        // More: every key the TV publishes.
        private ComboBox _allKeysBox;
        private string _allKeysSig = "";

        public MainWindow()
        {
            _settings = Settings.Load(_settingsPath);
            Strings.Current = Strings.FromCode(_settings.Language);
            _tv = new TvClient(_settings, _settingsPath);

            Width = 410;
            Height = 640;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            // Language, IP, tabs and status: the top and bottom stay visible on every tab.
            var top = new StackPanel { Spacing = 8 };
            var remote = new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };

            // --- Language + About ---
            _langBox = new ComboBox { Width = 170 };
            _langBox.ItemsSource = new[] { Strings.Get("LangEnUs"), Strings.Get("LangPtBr") };
            _langBox.SelectedIndex = Strings.Current == Lang.PtBr ? 1 : 0;
            _langBox.SelectionChanged += (s, e) => ChangeLanguage(_langBox.SelectedIndex == 1 ? Lang.PtBr : Lang.EnUs);
            var aboutBtn = new Button { Width = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
            Label(aboutBtn, "About");
            aboutBtn.Click += async (s, e) => await Safe(() => new AboutWindow().ShowDialog(this));
            top.Children.Add(Row(Text("LanguageLabel"), _langBox, aboutBtn));

            // --- TV IP + pairing ---
            _ipBox = new TextBox { Text = _settings.Ip, Width = 150, Watermark = Settings.DefaultIp };
            var pairBtn = new Button { Width = 140, HorizontalContentAlignment = HorizontalAlignment.Center };
            Label(pairBtn, "Pair");
            pairBtn.Click += async (s, e) => await Safe(Pair);
            top.Children.Add(Row(Text("TvIp"), _ipBox, pairBtn));

            // --- Power ---
            remote.Children.Add(Row(
                Btn("PowerOn", PowerOn),
                Key("PowerOff", "PowerOff"),
                VolKey("Mute", "Mute")));

            // --- Volume / Channel ---
            remote.Children.Add(Row(
                VolKey("VolDown", "VolumeDown"),
                VolKey("VolUp", "VolumeUp"),
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
            remote.Children.Add(grid);

            // --- Inputs (HDMI codes are not in the TV's list; standard BRAVIA codes) ---
            remote.Children.Add(Row(
                Key("Input", "Input"),
                Btn("Hdmi1", () => Ircc(IrccCodes.Hdmi1)),
                Btn("Hdmi2", () => Ircc(IrccCodes.Hdmi2))));
            remote.Children.Add(Row(
                Btn("Hdmi3", () => Ircc(IrccCodes.Hdmi3)),
                Btn("Hdmi4", () => Ircc(IrccCodes.Hdmi4)),
                Tip(Key("Ant", "Analog"), "AntTip")));

            // --- Open a page on the TV (needs pairing) ---
            _urlBox = new TextBox { Width = 230 };
            OnEnter(_urlBox, OpenPage);
            _relabel.Add(() => _urlBox.Watermark = Strings.Get("UrlHint"));
            var openBtn = new Button { Width = 130, HorizontalContentAlignment = HorizontalAlignment.Center };
            Label(openBtn, "OpenOnTv");
            openBtn.Click += async (s, e) => await Safe(OpenPage);
            remote.Children.Add(Row(_urlBox, openBtn));

            // --- Tabs: Remote | Numbers & colors | Media | Inputs | More ---
            // No theme padding: the widest rows (4 buttons of 88 px) need the full 382 px.
            StyleTabs();
            _tabs = new TabControl { Padding = new Thickness(0) };
            _tabs.Items.Add(Tab("TabRemote", remote));
            _tabs.Items.Add(Tab("TabNumbers", BuildNumbersPage()));
            _tabs.Items.Add(Tab("TabMedia", BuildMediaPage()));
            _tabs.Items.Add(Tab("TabInputs", BuildInputsPage()));
            _tabs.Items.Add(Tab("TabMore", BuildMorePage()));
            _tabs.SelectionChanged += async (s, e) =>
            {
                if (e.Source == _tabs) await Safe(OnTabChanged);
            };

            // --- Fixed navigation bar: the TV asks for RETURN, OPTIONS and DISPLAY on any screen ---
            var nav = Row(
                Tip(Key("Back", "Return"), "TipReturn"),
                Tip(Key("Home", "Home"), "TipHome"),
                Tip(Key("Options", "Options"), "TipOptions"),
                Tip(Key("Display", "Display"), "TipDisplay"));
            nav.Margin = new Thickness(0, 8, 0, 0);

            // --- Status ---
            _status = new TextBlock
            {
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var root = new Grid { Margin = new Thickness(14), RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto") };
            Place(root, top, 0, 0);
            Place(root, _tabs, 0, 1);
            Place(root, nav, 0, 2);
            Place(root, _status, 0, 3);

            Content = root;
            Closing += (s, e) => SaveSettings();

            _inputsTimer.Tick += async (s, e) => await Safe(() => RefreshInputs(true, true));
            _volReadTimer.Tick += async (s, e) => await Safe(AutoReadVolume);
            _volPumpTimer.Tick += async (s, e) => await Safe(FlushVolume);
            ApplyLanguage();
        }

        // Reads on their own run only with the right tab open and the window not minimized.
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == WindowStateProperty) UpdateTimers();
        }

        private void UpdateTimers()
        {
            bool shown = WindowState != WindowState.Minimized;
            SetTimer(_inputsTimer, shown && _tabs != null && _tabs.SelectedIndex == 3);
            SetTimer(_volReadTimer, shown && _tabs != null && _tabs.SelectedIndex == 4);
        }

        private static void SetTimer(DispatcherTimer timer, bool on)
        {
            if (on && !timer.IsEnabled) timer.Start();
            else if (!on && timer.IsEnabled) timer.Stop();
        }

        // Enter in a text box does what the matching button does. Tunnel: the TextBox may handle Enter itself.
        private void OnEnter(TextBox box, Func<Task> action)
        {
            box.AddHandler(InputElement.KeyDownEvent, async (s, e) =>
            {
                if (e.Key != Avalonia.Input.Key.Enter) return;
                e.Handled = true;
                await Safe(action);
            }, RoutingStrategies.Tunnel);
        }

        // Tooltip text from the dictionary, re-applied when the language changes.
        private Button Tip(Button b, string key)
        {
            _relabel.Add(() => ToolTip.SetTip(b, Strings.Get(key)));
            return b;
        }

        // ---------- tabs ----------

        private TabItem Tab(string key, Control content)
        {
            var item = new TabItem { Padding = new Thickness(6, 4), Content = content };
            _relabel.Add(() => item.Header = Strings.Get(key));
            return item;
        }

        // Tab headers that look like tabs: dark 14 pt text on a bordered light fill (at least 4.5:1),
        // and a bold, highlighted selected tab.
        private void StyleTabs()
        {
            Styles.Add(new Style(x => x.OfType<TabItem>())
            {
                Setters =
                {
                    new Setter(TemplatedControl.FontSizeProperty, 14.0),
                    new Setter(TemplatedControl.ForegroundProperty, new SolidColorBrush(Color.Parse("#1A1A1A"))),
                    new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(Color.Parse("#E6E6E6"))),
                    new Setter(TemplatedControl.BorderBrushProperty, new SolidColorBrush(Color.Parse("#707070"))),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
                    new Setter(Layoutable.MarginProperty, new Thickness(0, 0, 2, 2)),
                }
            });
            Styles.Add(new Style(x => x.OfType<TabItem>().Class(":selected"))
            {
                Setters =
                {
                    new Setter(TemplatedControl.ForegroundProperty, new SolidColorBrush(Color.Parse("#0A1F44"))),
                    new Setter(TemplatedControl.BackgroundProperty, new SolidColorBrush(Color.Parse("#9CC8FF"))),
                    new Setter(TemplatedControl.BorderBrushProperty, new SolidColorBrush(Color.Parse("#1F5FBF"))),
                    new Setter(TemplatedControl.BorderThicknessProperty, new Thickness(2)),
                    new Setter(TemplatedControl.FontWeightProperty, FontWeight.Bold),
                }
            });
        }

        private static StackPanel Page() => new StackPanel { Spacing = 8, Margin = new Thickness(0, 10, 0, 0) };

        // Keys on the new tabs are wider so three fit in a row.
        private Button PageKey(string labelKey, string irccName) => Btn(labelKey, () => SendNamed(irccName), 118);

        private Control BuildNumbersPage()
        {
            var page = Page();

            var pad = new Grid
            {
                Width = 270,
                Height = 200,
                HorizontalAlignment = HorizontalAlignment.Center,
                ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                RowDefinitions = new RowDefinitions("*,*,*,*")
            };
            for (int d = 1; d <= 9; d++) Place(pad, DBtn(d.ToString(), "Num" + d), (d - 1) % 3, (d - 1) / 3);
            Place(pad, DBtn(null, "Enter"), 0, 3);
            Place(pad, DBtn("0", "Num0"), 1, 3);
            Place(pad, DBtn(null, "Confirm"), 2, 3);
            page.Children.Add(pad);

            _colorRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center };
            RebuildColors();
            _relabel.Add(() =>
            {
                foreach (Button b in _colorRow.Children) b.Content = Strings.Get((string)b.Tag);
            });
            page.Children.Add(_colorRow);
            return page;
        }

        // The four color keys, in the order of _colorLayout.
        private void RebuildColors()
        {
            _colorRow.Children.Clear();
            foreach (var name in ColorKeys.Order(_colorLayout))
            {
                var irccName = name;
                var b = new Button
                {
                    Width = 88,
                    Tag = name,
                    Padding = new Thickness(4, 5),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    Background = name == "Red" ? Brushes.Firebrick : name == "Green" ? Brushes.ForestGreen
                        : name == "Yellow" ? Brushes.Gold : Brushes.RoyalBlue,
                    Foreground = name == "Yellow" ? Brushes.Black : Brushes.White,
                    Content = Strings.Get(name)
                };
                b.Click += async (s, e) => await Safe(() => SendNamed(irccName));
                _colorRow.Children.Add(b);
            }
        }

        private Control BuildMediaPage()
        {
            var page = Page();
            page.Children.Add(Row(PageKey("Play", "Play"), PageKey("Pause", "Pause"), PageKey("Stop", "Stop")));
            page.Children.Add(Row(PageKey("Rewind", "Rewind"), PageKey("Forward", "Forward")));
            page.Children.Add(Row(PageKey("Prev", "Prev"), PageKey("Next", "Next")));
            return page;
        }

        private Control BuildInputsPage()
        {
            var page = Page();
            page.Children.Add(Row(Btn("Refresh", () => RefreshInputs(false), 118)));
            _inputsPanel = new StackPanel { Spacing = 4 };
            page.Children.Add(_inputsPanel);
            _relabel.Add(RenderInputs);
            return page;
        }

        // One button per input the TV reported: its own title, and a green dot when connected.
        private void RenderInputs()
        {
            _inputsPanel.Children.Clear();
            if (_inputs.Count == 0)
            {
                _inputsPanel.Children.Add(new TextBlock { Text = Strings.Get("InputsEmpty"), Foreground = Brushes.Gray, TextWrapping = TextWrapping.Wrap });
                return;
            }
            foreach (var input in _inputs)
            {
                var chosen = input;
                var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                // Same fixed-width area on every row, so every name starts at the same place.
                var dot = new Ellipse
                {
                    Width = 10,
                    Height = 10,
                    Fill = chosen.Connected ? Brushes.ForestGreen : null,
                    Stroke = chosen.Connected ? Brushes.ForestGreen : Brushes.Gray,
                    StrokeThickness = 1.5,
                    VerticalAlignment = VerticalAlignment.Center
                };
                label.Children.Add(new Panel { Width = 14, Children = { dot } });
                label.Children.Add(new TextBlock { Text = chosen.Title });
                if (chosen.Connected)
                    label.Children.Add(new TextBlock { Text = "(" + Strings.Get("InputConnected") + ")", Foreground = Brushes.ForestGreen });
                var b = new Button
                {
                    Content = label,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                b.Click += async (s, e) => await Safe(() => SelectInput(chosen));
                _inputsPanel.Children.Add(b);
            }
        }

        private Control BuildMorePage()
        {
            var page = Page();
            page.Children.Add(Row(Btn("Guide", GuideKey, 118), PageKey("Subtitle", "SubTitle"), PageKey("Audio", "Audio")));
            page.Children.Add(Row(PageKey("Wide", "Wide"), PageKey("Exit", "Exit"), PageKey("PicOff", "PicOff")));
            page.Children.Add(Row(PageKey("SyncMenu", "SyncMenu")));

            // --- Exact volume: read-only without pairing; set when the slider is released ---
            _volSlider = new Slider
            {
                Minimum = 0,
                Maximum = 100,
                Width = 190,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                IsEnabled = false,
                VerticalAlignment = VerticalAlignment.Center
            };
            _volNumber = new TextBlock { Width = 34, Text = "–", TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            _volSlider.PropertyChanged += (s, e) =>
            {
                if (e.Property != RangeBase.ValueProperty) return;
                _volNumber.Text = ((int)Math.Round(_volSlider.Value)).ToString();
                if (_volFromTv || !_volHeld) return;
                _ = Safe(OfferVolume);
            };
            // Tunnel: the slider's own handling of the click or the key comes after these.
            _volSlider.AddHandler(InputElement.PointerPressedEvent, (s, e) => _volHeld = true, RoutingStrategies.Tunnel, true);
            _volSlider.AddHandler(InputElement.PointerReleasedEvent, async (s, e) => await Safe(ReleaseVolume), RoutingStrategies.Bubble, true);
            _volSlider.AddHandler(InputElement.PointerCaptureLostEvent, async (s, e) => await Safe(ReleaseVolume), RoutingStrategies.Bubble, true);
            _volSlider.AddHandler(InputElement.KeyDownEvent, (s, e) => { if (IsVolumeKey(e.Key)) _volHeld = true; }, RoutingStrategies.Tunnel, true);
            _volSlider.AddHandler(InputElement.KeyUpEvent, async (s, e) => { if (IsVolumeKey(e.Key)) await Safe(ReleaseVolume); }, RoutingStrategies.Bubble, true);
            _volNote = new TextBlock { Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap };
            _relabel.Add(ShowVolumeNote);
            page.Children.Add(Row(Text("VolumeExact"), _volSlider, _volNumber));
            page.Children.Add(_volNote);

            // --- Every key the TV publishes ---
            _allKeysBox = new ComboBox { Width = 170 };
            _allKeysBox.ItemsSource = AllKeys.Build(null);
            _relabel.Add(() => _allKeysBox.PlaceholderText = Strings.Get("ChooseKey"));
            page.Children.Add(Row(Text("AllKeysLabel"), _allKeysBox, Btn("SendKey", SendSelectedKey, 70)));

            // --- Type text (needs pairing) ---
            _textBox = new TextBox { Width = 232 };
            OnEnter(_textBox, SendText);
            _relabel.Add(() => _textBox.Watermark = Strings.Get("TextHint"));
            page.Children.Add(Row(_textBox, Btn("SendText", SendText, 118)));

            // --- TV clock (needs pairing) ---
            page.Children.Add(Row(Btn("SyncClock", SyncClock, 200)));
            return page;
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

        private Button Btn(string key, Func<Task> action, double width = 88)
        {
            var b = new Button { Width = width, HorizontalContentAlignment = HorizontalAlignment.Center };
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
            // The MAC is per TV: a TV at a new IP needs its own, even if another IP has one.
            bool needCodes = _codes == null, needMac = _tv.Mac == null;
            if (!needCodes && !needMac) return;
            if (DateTime.UtcNow - _infoFailedUtc < InfoRetry) return;

            if (needCodes)
            {
                var r = await _tv.QuickScalar("system", "getRemoteControllerInfo");
                if (r.Status == 200) _codes = IrccCodes.Parse(r.Body);
                if (_codes == null) _infoFailedUtc = DateTime.UtcNow;
                if (r.Status == 0) return;
            }
            if (needMac && await _tv.RefreshMac(TvClient.QuickTimeout) == null) _infoFailedUtc = DateTime.UtcNow;
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
                var pairedIp = _tv.Ip;

                var first = await _tv.ActRegister(null, false);
                if (first.Status == 0) { SetStatus("NoResponse", true); return; }

                // Already registered: the TV answers 200 with a cookie right away.
                if (!_tv.SaveCookie(first))
                {
                    if (first.Status != 401) { SetStatus("PairFailed", true, first.Status); return; }

                    var pin = await new PinWindow().ShowDialog<string>(this);
                    // The PIN belongs to the TV that showed it; don't send it to another IP.
                    if (pin == null || _tv.Ip != pairedIp) { SetStatus("PairCancelled", true); return; }

                    var second = await _tv.ActRegister(pin, false);
                    if (second.Status == 0) { SetStatus("NoResponse", true); return; }
                    if (!_tv.SaveCookie(second)) { SetStatus("PairFailed", true, second.Status); return; }
                }

                // The IP may have been changed while the PIN dialog was open: report the TV that paired.
                await _tv.RefreshMac(TvClient.QuickTimeout);
                var until = _settings.GetAuthExpiresUtc(pairedIp);
                if (until != null) SetStatus("Paired", false, until.Value.ToLocalTime());
                await RefreshVolume();   // the exact-volume slider unlocks once paired
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

                // Wake-on-LAN goes out first with the MAC saved for this IP. Without one, ask this
                // TV directly (no retry back-off; the KDL-42W805A still answers on port 80 in standby).
                var mac = _tv.Mac ?? await _tv.RefreshMac(TvClient.NormalTimeout);

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

        // ---------- 1.2.0 actions ----------

        // Points the client at the typed IP without any message: for reads that happen on their own.
        private bool UseIpQuiet()
        {
            var ip = Ip();
            if (ip.Length == 0 || !UrlHelper.IsValidHost(ip)) return false;
            _tv.Ip = ip;
            return true;
        }

        // One place that turns a TV reply into a status line. True when the call worked.
        // okKey null leaves the current status alone.
        private bool ShowOutcome(TvReply r, string okKey, params object[] okArgs)
        {
            switch (ScalarReply.Classify(r.Status, r.Body))
            {
                case ScalarOutcome.Ok:
                    if (okKey != null) SetStatus(okKey, false, okArgs);
                    return true;
                case ScalarOutcome.NoResponse: SetStatus("NoResponse", true); break;
                case ScalarOutcome.NotPaired: SetStatus("NotPaired", true); break;
                case ScalarOutcome.NotSupported: SetStatus("NotSupported", true); break;
                case ScalarOutcome.TvError: SetStatus("TvError", true, ScalarReply.ErrorCode(r.Body)); break;
                default: SetStatus("HttpFailed", true, r.Status); break;
            }
            return false;
        }

        private async Task OnTabChanged()
        {
            UpdateTimers();
            switch (_tabs.SelectedIndex)
            {
                case 1: await LoadColorLayout(); break;
                case 3: await RefreshInputs(true); break;
                case 4:
                    await RefreshVolume(true);
                    await LoadAllKeys();
                    break;
            }
        }

        // Vol -/+ and Mute send the same IRCC key as before; afterwards the slider reads the new volume.
        private Button VolKey(string key, string irccName) => Btn(key, async () =>
        {
            await SendNamed(irccName);
            await Task.Delay(400);
            await RefreshVolume();
        });

        private async Task GuideKey()
        {
            if (!UseIp()) return;
            await LoadTvInfo();
            await Ircc(IrccCodes.ResolveGuide(_codes));
        }

        // The TV says how its color keys are laid out; until it does the default RGYB shows.
        private async Task LoadColorLayout()
        {
            if (!UseIpQuiet()) return;
            var ip = _tv.Ip;
            if (_colorIp == ip) return;
            var r = await _tv.GetColorLayout();
            var layout = r.Ok ? ColorKeys.ParseLayout(r.Body) : null;
            if (layout == null || _tv.Ip != ip) return;
            _colorIp = ip;
            _colorLayout = layout;
            RebuildColors();
        }

        // quiet: no IP messages. auto: the automatic 3 s refresh: no status line on failure, and
        // the list is redrawn only when it changed.
        private async Task RefreshInputs(bool quiet, bool auto = false)
        {
            if (auto && !AutoRead.InputsAllowed(_inputsReading)) return;
            if (quiet ? !UseIpQuiet() : !UseIp()) return;
            var ip = _tv.Ip;
            if (_inputsIp != ip)
            {
                _inputsIp = ip;
                _inputs = new List<TvInput>();
                RenderInputs();
            }
            _inputsReading = true;
            try
            {
                var r = await _tv.GetInputs();
                if (_tv.Ip != ip) return;
                if (r.Ok)
                {
                    var fresh = InputList.Parse(r.Body);
                    if (auto && InputList.SameList(_inputs, fresh)) return;
                    _inputs = fresh;
                    RenderInputs();
                }
                else if (!auto) ShowOutcome(r, null);
            }
            finally
            {
                _inputsReading = false;
            }
        }

        // Paired: switch with setPlayContent. Unpaired: only HDMI1-HDMI4 can, with the IRCC codes.
        private async Task SelectInput(TvInput input)
        {
            if (!UseIp()) return;
            await _tv.RenewIfNeeded();
            if (_tv.IsPaired)
            {
                ShowOutcome(await _tv.PlayInput(input.Uri), "InputSwitched", input.Title);
                return;
            }
            var code = InputList.IrccFallback(input.Uri);
            if (code != null) await Ircc(code);
            else SetStatus("NotPaired", true);
        }

        // report: say why when the read fails (opening the tab). After a Vol/Mute click the
        // status of that click stays untouched.
        private async Task RefreshVolume(bool report = false)
        {
            if (!UseIpQuiet()) return;
            var ip = _tv.Ip;
            _volReading = true;
            try
            {
                var r = await _tv.GetVolume();
                if (_tv.Ip != ip) return;
                ApplyVolume(r.Ok ? VolumeInfo.Parse(r.Body) : null);
                if (report && !r.Ok) ShowOutcome(r, null);
            }
            finally
            {
                _volReading = false;
            }
        }

        // Every 2 s while the More tab is open: follows the remote control. Silent on failure.
        private async Task AutoReadVolume()
        {
            TimeSpan? since = _volLastSendAt == null ? (TimeSpan?)null : _clock.Elapsed - _volLastSendAt.Value;
            if (!AutoRead.VolumeAllowed(_volHeld, since, _volReading || _volSending)) return;
            if (!UseIpQuiet()) return;
            var ip = _tv.Ip;
            _volReading = true;
            try
            {
                var r = await _tv.GetVolume();
                if (_tv.Ip != ip || !r.Ok) return;
                var info = VolumeInfo.Parse(r.Body);
                // Held or sent to while the reply was on its way: this reading is already old.
                if (info == null || _volHeld || _volSending || _volThrottle.HasPending) return;
                if (_volLastSendAt != null && _clock.Elapsed - _volLastSendAt.Value < AutoRead.VolumeQuietAfterSend) return;
                ApplyVolume(info);
            }
            finally
            {
                _volReading = false;
            }
        }

        private void ApplyVolume(VolumeInfo info)
        {
            bool paired = _tv.IsPaired;
            _volReadOnly = !paired;
            _volFromTv = true;   // a value that came from the TV is never sent back
            try
            {
                if (info == null)
                {
                    _volSlider.IsEnabled = false;
                    _volNumber.Text = "–";
                    _volApplied = -1;
                }
                else
                {
                    _volSlider.Maximum = info.Max;
                    _volSlider.Value = info.Volume;
                    _volNumber.Text = info.Volume.ToString();
                    _volApplied = info.Volume;
                    _volSlider.IsEnabled = paired;
                }
            }
            finally
            {
                _volFromTv = false;
            }
            _volThrottle.Reset(_volApplied);
            ShowVolumeNote();
        }

        private void ShowVolumeNote() => _volNote.Text = _volReadOnly ? Strings.Get("NotPaired") : "";

        private static bool IsVolumeKey(Avalonia.Input.Key k) =>
            k == Avalonia.Input.Key.Left || k == Avalonia.Input.Key.Right || k == Avalonia.Input.Key.Up || k == Avalonia.Input.Key.Down
            || k == Avalonia.Input.Key.PageUp || k == Avalonia.Input.Key.PageDown || k == Avalonia.Input.Key.Home || k == Avalonia.Input.Key.End;

        private int SliderVolume() => VolumeInfo.Clamp((int)Math.Round(_volSlider.Value), (int)_volSlider.Maximum);

        // The slider moved while held: send now if 250 ms have passed, else the pump timer sends it.
        private async Task OfferVolume()
        {
            if (!_volSlider.IsEnabled) return;
            var v = _volThrottle.Offer(SliderVolume(), _clock.Elapsed);
            if (v != null) await SendVolume(v.Value);
            if (_volThrottle.HasPending) SetTimer(_volPumpTimer, true);
        }

        // Timer tick while the slider is held: the waiting value, once 250 ms have passed.
        private async Task FlushVolume()
        {
            var v = _volThrottle.Take(_clock.Elapsed);
            if (v != null) await SendVolume(v.Value);
            if (!_volThrottle.HasPending) SetTimer(_volPumpTimer, false);
        }

        // Mouse, thumb or key released: send the final value if it is not the last one sent.
        private async Task ReleaseVolume()
        {
            if (!_volHeld) return;
            _volHeld = false;
            SetTimer(_volPumpTimer, false);
            if (!_volSlider.IsEnabled) return;
            if (_volSending) { _volReleaseQueued = true; return; }
            var v = _volThrottle.Release(SliderVolume(), _clock.Elapsed);
            if (v != null) await SendVolume(v.Value);
        }

        // One send at a time. When it ends, a release that came in meanwhile is handled.
        private async Task SendVolume(int volume)
        {
            if (_volSending)
            {
                // Should not happen (the throttle spaces sends), but never lose the value.
                _volReleaseQueued = true;
                return;
            }
            _volSending = true;
            bool failed = false;
            try
            {
                if (!UseIp()) { failed = true; return; }
                await _tv.RenewIfNeeded();
                if (!_tv.IsPaired) { SetStatus("NotPaired", true); failed = true; return; }
                _volLastSendAt = _clock.Elapsed;
                if (!ShowOutcome(await _tv.SetVolume(volume), "VolumeSet", volume)) { failed = true; return; }
                _volLastSendAt = _clock.Elapsed;
                _volApplied = volume;
            }
            finally
            {
                _volSending = false;
            }
            if (failed) { await RefreshVolume(); return; }
            if (_volReleaseQueued)
            {
                _volReleaseQueued = false;
                var last = _volThrottle.Release(SliderVolume(), _clock.Elapsed);
                if (last != null) await SendVolume(last.Value);
            }
        }

        // Asks the TV first: setTextForm only makes sense with a text field open on screen.
        private async Task SendText()
        {
            if (!UseIp()) return;
            var result = await TextForm.Send(_tv, _textBox.Text ?? "");
            switch (result.Kind)
            {
                case TextInputKind.FieldOff: SetStatus("TextFieldOff", true); break;
                case TextInputKind.NotSupported: SetStatus("NotSupported", true); break;
                case TextInputKind.NotPaired: SetStatus("NotPaired", true); break;
                case TextInputKind.Called: ShowOutcome(result.Reply, "TextSent"); break;
                default: ShowOutcome(result.Reply, null); break;
            }
        }

        // Fills "All TV keys" with the TV's own names (built-in table when the TV gave none).
        private async Task LoadAllKeys()
        {
            if (UseIpQuiet()) await LoadTvInfo();
            var names = AllKeys.Build(_codes?.Keys);
            var sig = string.Join("|", names);
            if (sig == _allKeysSig) return;
            _allKeysSig = sig;
            var chosen = _allKeysBox.SelectedItem as string;
            _allKeysBox.ItemsSource = names;
            if (chosen != null && names.Contains(chosen)) _allKeysBox.SelectedItem = chosen;
        }

        private async Task SendSelectedKey()
        {
            var name = _allKeysBox.SelectedItem as string;
            if (name == null) return;
            await SendNamed(name);
        }

        private async Task SyncClock()
        {
            if (!UseIp()) return;
            await _tv.RenewIfNeeded();
            if (!ShowOutcome(await _tv.SetClock(DateTimeOffset.Now, TimeZoneInfo.Local), null)) return;

            var now = await _tv.GetCurrentTime();
            var shown = now.Ok ? ScalarReply.CurrentTime(now.Body) : null;
            if (shown != null) SetStatus("ClockSetRead", false, shown);
            else SetStatus("ClockSet", false);
        }
    }
}