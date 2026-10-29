using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace SonyTvRemote
{
    // Version, repository link and version history, in the current language.
    public class AboutWindow : Window
    {
        public AboutWindow()
        {
            Title = Strings.Get("AboutTitle");
            Width = 520;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var root = new DockPanel { Margin = new Thickness(16) };

            var header = new StackPanel { Spacing = 6 };
            header.Children.Add(new TextBlock
            {
                Text = Strings.Get("TitleWithVersion", Strings.Get("AppTitle"), AppInfo.Version),
                FontSize = 18,
                FontWeight = FontWeight.SemiBold
            });
            header.Children.Add(new TextBlock { Text = Strings.Get("Version", AppInfo.Version) });

            var link = new TextBlock
            {
                Text = AppInfo.RepoUrl,
                Foreground = Brushes.RoyalBlue,
                TextDecorations = TextDecorations.Underline,
                Cursor = new Cursor(StandardCursorType.Hand)
            };
            link.PointerPressed += (s, e) => OpenInBrowser(AppInfo.RepoUrl);
            var repoRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
            repoRow.Children.Add(new TextBlock { Text = Strings.Get("Repository") });
            repoRow.Children.Add(link);
            header.Children.Add(repoRow);

            header.Children.Add(new TextBlock
            {
                Text = Strings.Get("History"),
                FontWeight = FontWeight.SemiBold,
                Margin = new Thickness(0, 10, 0, 0)
            });
            DockPanel.SetDock(header, Dock.Top);
            root.Children.Add(header);

            var close = new Button
            {
                Content = Strings.Get("Close"),
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            close.Click += (s, e) => Close();
            DockPanel.SetDock(close, Dock.Bottom);
            root.Children.Add(close);

            root.Children.Add(new ScrollViewer
            {
                Margin = new Thickness(0, 6, 0, 0),
                Content = new SelectableTextBlock { Text = Strings.Get("ChangelogText"), TextWrapping = TextWrapping.Wrap }
            });

            Content = root;
        }

        private static void OpenInBrowser(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception) { }
        }
    }

    // Searches the network when it opens and lists the Sony TVs it finds, one button each.
    // Returns the TV clicked, or null. Closing the dialog cancels the search.
    public class FindWindow : Window
    {
        private readonly Settings _settings;
        private readonly string _currentIp;
        private readonly TvClient _tv;
        private readonly TextBlock _message;
        private readonly StackPanel _list = new StackPanel { Spacing = 4 };
        private readonly Button _again;
        private CancellationTokenSource _cts;

        public FindWindow(Settings settings, string currentIp, TvClient tv)
        {
            _settings = settings;
            _currentIp = currentIp;
            _tv = tv;

            Title = Strings.Get("FindTitle");
            Width = 380;
            Height = 420;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            _message = new TextBlock { TextWrapping = TextWrapping.Wrap };
            _again = new Button { Content = Strings.Get("FindAgain") };
            _again.Click += async (s, e) => await Search();
            var close = new Button { Content = Strings.Get("Close"), IsCancel = true };
            close.Click += (s, e) => Close(null);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 10, 0, 0)
            };
            footer.Children.Add(_again);
            footer.Children.Add(close);

            var root = new DockPanel { Margin = new Thickness(14) };
            DockPanel.SetDock(_message, Dock.Top);
            DockPanel.SetDock(footer, Dock.Bottom);
            root.Children.Add(_message);
            root.Children.Add(footer);
            root.Children.Add(new ScrollViewer { Margin = new Thickness(0, 8, 0, 0), Content = _list });
            Content = root;

            Opened += async (s, e) => await Search();
            Closing += (s, e) => _cts?.Cancel();
        }

        private async Task Search()
        {
            _cts?.Cancel();
            var cts = _cts = new CancellationTokenSource();
            _again.IsEnabled = false;
            _list.Children.Clear();
            _message.Text = Strings.Get("FindSearching");
            try
            {
                var tvs = await Finder.FindAll(NetDiscovery.Parts(_tv), cts.Token);
                if (cts != _cts) return;
                Show(tvs);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                if (cts == _cts) Show(new List<FoundTv>());
            }
            if (cts == _cts) _again.IsEnabled = true;
        }

        private void Show(List<FoundTv> tvs)
        {
            _message.Text = Strings.Get(tvs.Count == 0 ? "FindNone" : "FindPick");
            foreach (var tv in tvs)
            {
                var chosen = tv;
                var first = chosen.Name + " — " + chosen.Ip;
                if (chosen.Mac != null && _settings.FindIpByMac(chosen.Mac) != null) first += " " + Strings.Get("FindSaved");
                if (string.Equals(chosen.Ip, _currentIp, StringComparison.OrdinalIgnoreCase)) first += " " + Strings.Get("FindCurrent");

                var label = new StackPanel();
                label.Children.Add(new TextBlock { Text = first, TextWrapping = TextWrapping.Wrap });
                if (!string.IsNullOrEmpty(chosen.Model))
                    label.Children.Add(new TextBlock { Text = chosen.Model, Foreground = Brushes.Gray });
                var b = new Button
                {
                    Content = label,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Left
                };
                b.Click += (s, e) => Close(chosen);
                _list.Children.Add(b);
            }
        }
    }

    // Asks for the 4-digit PIN the TV shows. Returns the PIN, or null when cancelled.
    public class PinWindow : Window
    {
        public PinWindow()
        {
            Title = Strings.Get("PinTitle");
            Width = 340;
            Height = 170;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;

            var pinBox = new TextBox { MaxLength = 4, Width = 90, FontSize = 18 };
            var ok = new Button { Content = Strings.Get("Ok"), Width = 90, IsEnabled = false, IsDefault = true };
            var cancel = new Button { Content = Strings.Get("Cancel"), Width = 90, IsCancel = true };

            pinBox.TextChanged += (s, e) =>
            {
                var t = pinBox.Text ?? "";
                ok.IsEnabled = t.Length == 4 && t.All(c => c >= '0' && c <= '9');
            };
            ok.Click += (s, e) => Close(pinBox.Text);
            cancel.Click += (s, e) => Close(null);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttons.Children.Add(ok);
            buttons.Children.Add(cancel);

            var root = new StackPanel { Margin = new Thickness(16), Spacing = 10 };
            root.Children.Add(new TextBlock { Text = Strings.Get("PinPrompt"), TextWrapping = TextWrapping.Wrap });
            root.Children.Add(pinBox);
            root.Children.Add(buttons);
            Content = root;

            Opened += (s, e) => pinBox.Focus();
        }
    }
}
