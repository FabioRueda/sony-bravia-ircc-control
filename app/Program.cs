using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

// Network remote control for Sony BRAVIA TVs (2010-2013 generation).
// IRCC-IP (remote control) + Scalar Web API (state, pairing, open a web page).
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
}
