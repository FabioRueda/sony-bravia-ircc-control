using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

// Network remote control for Sony BRAVIA TVs (2010-2013 generation).
// IRCC-IP (remote control) + Scalar Web API (open a web page). No password.
// UI built with Avalonia so it cross-compiles from Linux to Windows.

namespace SonyTvRemote
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
            => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>().UsePlatformDetect();
    }

    public class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.MainWindow = new MainWindow();
            base.OnFrameworkInitializationCompleted();
        }
    }

    public class MainWindow : Window
    {
        private static readonly HttpClient Http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private readonly TextBox _ipBox;
        private readonly TextBox _urlBox;
        private readonly TextBlock _status;

        public MainWindow()
        {
            Title = "Sony BRAVIA Remote (network)";
            Width = 410;
            Height = 600;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var root = new StackPanel { Margin = new Thickness(14), Spacing = 8 };

            // --- TV IP ---
            _ipBox = new TextBox { Text = "192.168.1.100", Width = 190, Watermark = "TV IP" };
            var ipRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            ipRow.Children.Add(new TextBlock { Text = "TV IP:", VerticalAlignment = VerticalAlignment.Center });
            ipRow.Children.Add(_ipBox);
            root.Children.Add(ipRow);

            // --- Power ---
            root.Children.Add(Row(
                Btn("Power On",  "AAAAAQAAAAEAAAAuAw=="),
                Btn("Power Off", "AAAAAQAAAAEAAAAvAw=="),
                Btn("Mute",      "AAAAAQAAAAEAAAAUAw==")));

            // --- Volume / Channel ---
            root.Children.Add(Row(
                Btn("Vol -", "AAAAAQAAAAEAAAATAw=="),
                Btn("Vol +", "AAAAAQAAAAEAAAASAw=="),
                Btn("Ch -",  "AAAAAQAAAAEAAAARAw=="),
                Btn("Ch +",  "AAAAAQAAAAEAAAAQAw==")));

            // --- D-pad ---
            var grid = new Grid
            {
                Width = 190,
                Height = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                ColumnDefinitions = new ColumnDefinitions("*,*,*"),
                RowDefinitions = new RowDefinitions("*,*,*")
            };
            Place(grid, DBtn("▲", "AAAAAQAAAAEAAAB0Aw=="), 1, 0);
            Place(grid, DBtn("◄", "AAAAAQAAAAEAAAA0Aw=="), 0, 1);
            Place(grid, DBtn("OK", "AAAAAQAAAAEAAABlAw=="), 1, 1);
            Place(grid, DBtn("►", "AAAAAQAAAAEAAAAzAw=="), 2, 1);
            Place(grid, DBtn("▼", "AAAAAQAAAAEAAAB1Aw=="), 1, 2);
            root.Children.Add(grid);

            // --- Navigation ---
            root.Children.Add(Row(
                Btn("Back",    "AAAAAQAAAAEAAABjAw=="),
                Btn("Home",    "AAAAAQAAAAEAAABgAw=="),
                Btn("Options", "AAAAAQAAAAEAAAA2Aw==")));

            // --- Inputs ---
            root.Children.Add(Row(
                Btn("Input", "AAAAAQAAAAEAAAAlAw=="),
                Btn("HDMI1", "AAAAAgAAABoAAABaAw=="),
                Btn("HDMI2", "AAAAAgAAABoAAABbAw==")));
            root.Children.Add(Row(
                Btn("HDMI3", "AAAAAgAAABoAAABcAw=="),
                Btn("HDMI4", "AAAAAgAAABoAAABdAw==")));

            // --- Open a page on the TV ---
            _urlBox = new TextBox { Text = "https://", Width = 230, Watermark = "URL" };
            var openBtn = new Button { Content = "Open on TV", Width = 100 };
            openBtn.Click += async (s, e) => await OpenPage(_urlBox.Text);
            var urlRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            urlRow.Children.Add(_urlBox);
            urlRow.Children.Add(openBtn);
            root.Children.Add(urlRow);

            // --- Status ---
            _status = new TextBlock
            {
                Text = "Ready. Set the TV IP and click.",
                Foreground = Brushes.Gray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            root.Children.Add(_status);

            Content = root;
        }

        private Button Btn(string text, string code)
        {
            var b = new Button { Content = text, Width = 88, HorizontalContentAlignment = HorizontalAlignment.Center };
            b.Click += async (s, e) => await Ircc(code);
            return b;
        }

        private Button DBtn(string text, string code)
        {
            var b = new Button
            {
                Content = text,
                Margin = new Thickness(3),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                FontSize = 15
            };
            b.Click += async (s, e) => await Ircc(code);
            return b;
        }

        private StackPanel Row(params Control[] items)
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

        private string Ip() => (_ipBox.Text ?? "").Trim();

        // Send an IRCC remote-control code via SOAP
        private async Task Ircc(string code)
        {
            var ip = Ip();
            if (ip.Length == 0) { SetStatus("Enter the TV IP.", true); return; }

            var soap =
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>" +
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">" +
                "<s:Body><u:X_SendIRCC xmlns:u=\"urn:schemas-sony-com:service:IRCC:1\">" +
                "<IRCCCode>" + code + "</IRCCCode></u:X_SendIRCC></s:Body></s:Envelope>";

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, "http://" + ip + "/sony/IRCC");
                req.Content = new StringContent(soap, Encoding.UTF8, "text/xml");
                req.Headers.TryAddWithoutValidation("SOAPAction", "\"urn:schemas-sony-com:service:IRCC:1#X_SendIRCC\"");
                var resp = await Http.SendAsync(req);
                SetStatus(resp.IsSuccessStatusCode
                    ? "OK (" + (int)resp.StatusCode + ")"
                    : "Failed: HTTP " + (int)resp.StatusCode, !resp.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                SetStatus("No response (TV off or off-network?): " + ex.Message, true);
            }
        }

        // Open a URL on the TV's browser via the Scalar Web API
        private async Task OpenPage(string url)
        {
            var ip = Ip();
            if (ip.Length == 0) { SetStatus("Enter the TV IP.", true); return; }
            url = (url ?? "").Trim();
            if (url.Length == 0 || url == "https://") { SetStatus("Enter a URL.", true); return; }

            var json = "{\"method\":\"setActiveApp\",\"id\":1,\"version\":\"1.0\",\"params\":[{\"uri\":\"localapp://webappruntime?url=" + url + "\"}]}";
            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var resp = await Http.PostAsync("http://" + ip + "/sony/appControl", content);
                SetStatus(resp.IsSuccessStatusCode
                    ? "Page sent to the TV."
                    : "Failed: HTTP " + (int)resp.StatusCode, !resp.IsSuccessStatusCode);
            }
            catch (Exception ex)
            {
                SetStatus("No response: " + ex.Message, true);
            }
        }

        private void SetStatus(string msg, bool error)
        {
            _status.Foreground = error ? Brushes.Firebrick : Brushes.ForestGreen;
            _status.Text = msg;
        }
    }
}
