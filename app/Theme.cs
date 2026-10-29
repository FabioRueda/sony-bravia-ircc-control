using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace SonyTvRemote
{
    // The navy and orange theme: every color, font and corner of the UI lives here.
    public static class AppTheme
    {
        // ---------- colors ----------
        public static readonly Color Navy = Color.Parse("#131B59");
        public static readonly Color NavyDark = Color.Parse("#0B1140");
        public static readonly Color NavyLight = Color.Parse("#E7E9F3");
        public static readonly Color Orange = Color.Parse("#FF9B00");
        public static readonly Color OrangeHover = Color.Parse("#E68A00");
        public static readonly Color OrangeText = Color.Parse("#A84E00");
        public static readonly Color Ink = Color.Parse("#1E2235");
        public static readonly Color Muted = Color.Parse("#565D73");
        public static readonly Color Line = Color.Parse("#D9DCE5");
        public static readonly Color Mist = Color.Parse("#F3F4F8");
        public static readonly Color White = Color.Parse("#FFFFFF");
        public static readonly Color Success = Color.Parse("#1E7A46");
        public static readonly Color Error = Color.Parse("#B42318");

        // The remote control's own key colors (not part of the theme), with their label colors.
        public static readonly Color KeyRed = Color.Parse("#B22222");
        public static readonly Color KeyGreen = Color.Parse("#1E7A46");
        public static readonly Color KeyYellow = Color.Parse("#FFD700");
        public static readonly Color KeyBlue = Color.Parse("#4169E1");
        public static readonly Color KeyLightText = Color.Parse("#FFFFFF");
        public static readonly Color KeyDarkText = Color.Parse("#000000");

        public static readonly IBrush NavyBrush = new SolidColorBrush(Navy);
        public static readonly IBrush NavyDarkBrush = new SolidColorBrush(NavyDark);
        public static readonly IBrush NavyLightBrush = new SolidColorBrush(NavyLight);
        public static readonly IBrush OrangeBrush = new SolidColorBrush(Orange);
        public static readonly IBrush OrangeHoverBrush = new SolidColorBrush(OrangeHover);
        public static readonly IBrush OrangeTextBrush = new SolidColorBrush(OrangeText);
        public static readonly IBrush InkBrush = new SolidColorBrush(Ink);
        public static readonly IBrush MutedBrush = new SolidColorBrush(Muted);
        public static readonly IBrush LineBrush = new SolidColorBrush(Line);
        public static readonly IBrush MistBrush = new SolidColorBrush(Mist);
        public static readonly IBrush WhiteBrush = new SolidColorBrush(White);
        public static readonly IBrush SuccessBrush = new SolidColorBrush(Success);
        public static readonly IBrush ErrorBrush = new SolidColorBrush(Error);

        public static IBrush KeyBrush(string name)
            => new SolidColorBrush(name == "Red" ? KeyRed : name == "Green" ? KeyGreen : name == "Yellow" ? KeyYellow : KeyBlue);

        public static IBrush KeyTextBrush(string name)
            => new SolidColorBrush(name == "Yellow" ? KeyDarkText : KeyLightText);

        // ---------- fonts ----------
        // Poppins 600 / 700 for titles, buttons and tabs; Roboto 400 for the rest. Arial if they cannot load.
        // The files are embedded in the .exe (app/Assets/Fonts). Both Poppins files report the family "Poppins".
        public const string FontBase = "avares://SonyTvRemote/Assets/Fonts";
        public static readonly FontFamily Poppins = FontFamily.Parse(FontBase + "#Poppins, Arial");
        public static readonly FontFamily Roboto = FontFamily.Parse(FontBase + "#Roboto, Arial");

        // ---------- shapes ----------
        public static readonly CornerRadius FieldRadius = new CornerRadius(6);
        public static readonly CornerRadius ButtonRadius = new CornerRadius(10);
        public static readonly CornerRadius TabRadius = new CornerRadius(6);

        // Light only: the theme has no dark version, and a dark Windows would mix Fluent's dark colors in.
        public static void Apply(Application app)
        {
            app.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light;
            AddResources(app.Resources);
            AddStyles(app.Styles);
        }

        // Fluent colors its controls from resources; these replace them (accent and every state).
        private static void AddResources(IResourceDictionary r)
        {
            // Accent (volume bar, list selection, focus): Navy, with NavyDark for the darker variants.
            r["SystemAccentColor"] = Navy;
            r["SystemAccentColorDark1"] = NavyDark;
            r["SystemAccentColorDark2"] = NavyDark;
            r["SystemAccentColorDark3"] = NavyDark;
            r["SystemAccentColorLight1"] = Navy;
            r["SystemAccentColorLight2"] = Navy;
            r["SystemAccentColorLight3"] = Navy;
            r["SystemControlFocusVisualPrimaryBrush"] = NavyBrush;
            r["SystemControlFocusVisualSecondaryBrush"] = WhiteBrush;

            r["ContentControlThemeFontFamily"] = Roboto;
            r["SystemControlForegroundBaseHighBrush"] = InkBrush;

            // Buttons.
            foreach (var state in new[] { "", "PointerOver", "Pressed" })
            {
                r["ButtonForeground" + state] = NavyBrush;
                r["ButtonBorderBrush" + state] = NavyBrush;
            }
            r["ButtonBackground"] = WhiteBrush;
            r["ButtonBackgroundPointerOver"] = MistBrush;
            r["ButtonBackgroundPressed"] = NavyLightBrush;
            r["ButtonBackgroundDisabled"] = WhiteBrush;
            r["ButtonForegroundDisabled"] = MutedBrush;
            r["ButtonBorderBrushDisabled"] = LineBrush;

            // Tabs.
            foreach (var state in new[] { "", "PointerOver", "Pressed" })
            {
                r["TabItemHeaderBackground" + state] = MistBrush;
                r["TabItemHeaderForeground" + state] = NavyBrush;
            }
            r["TabItemHeaderBackgroundSelected"] = NavyBrush;
            r["TabItemHeaderBackgroundSelectedPointerOver"] = NavyDarkBrush;
            r["TabItemHeaderBackgroundSelectedPressed"] = NavyDarkBrush;
            foreach (var state in new[] { "Selected", "SelectedPointerOver", "SelectedPressed" })
                r["TabItemHeaderForeground" + state] = WhiteBrush;

            // Text boxes.
            foreach (var state in new[] { "", "PointerOver", "Focused" })
            {
                r["TextControlBackground" + state] = WhiteBrush;
                r["TextControlForeground" + state] = InkBrush;
                r["TextControlBorderBrush" + state] = state == "Focused" ? NavyBrush : MutedBrush;
            }
            r["TextControlPlaceholderForeground"] = MutedBrush;
            r["TextControlBorderThemeThickness"] = new Thickness(1);
            r["TextControlBorderThemeThicknessFocused"] = new Thickness(2);

            // Combo boxes and lists.
            foreach (var state in new[] { "", "PointerOver", "Pressed", "Focused" })
            {
                r["ComboBoxBackground" + state] = state == "PointerOver" ? MistBrush : WhiteBrush;
                r["ComboBoxForeground" + state] = InkBrush;
                r["ComboBoxBorderBrush" + state] = state == "Focused" ? NavyBrush : MutedBrush;
            }
            r["ComboBoxDropDownBackground"] = WhiteBrush;
            r["ComboBoxDropDownBorderBrush"] = LineBrush;
            foreach (var item in new[] { "ComboBoxItem", "ListBoxItem" })
            {
                r[item + "Foreground"] = InkBrush;
                r[item + "ForegroundPointerOver"] = InkBrush;
                r[item + "ForegroundSelected"] = InkBrush;
                r[item + "ForegroundSelectedPointerOver"] = InkBrush;
                r[item + "BackgroundPointerOver"] = MistBrush;
                r[item + "BackgroundSelected"] = NavyLightBrush;
                r[item + "BackgroundSelectedPointerOver"] = NavyLightBrush;
                r[item + "BackgroundSelectedPressed"] = NavyLightBrush;
            }
        }

        private static Setter S(AvaloniaProperty p, object v) => new Setter(p, v);

        // Keyboard focus: a Navy ring with a thin white ring inside, so it also shows on a Navy fill.
        private static FuncTemplate<Control> FocusRing(double radius) => new FuncTemplate<Control>(() => new Border
        {
            Margin = new Thickness(1),
            BorderBrush = NavyBrush,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(radius),
            IsHitTestVisible = false,
            Child = new Border
            {
                BorderBrush = WhiteBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius > 1 ? radius - 2 : 0)
            }
        });

        private static Selector Part<T>(Selector button, string pseudo, string part) where T : Control
            => button.Class(pseudo).Template().OfType<T>().Name(part);

        private static void AddStyles(Styles s)
        {
            // Windows (also the About, Find and PIN dialogs).
            s.Add(new Style(x => x.OfType<Window>())
            {
                Setters = { S(Window.BackgroundProperty, WhiteBrush), S(Window.ForegroundProperty, InkBrush), S(Window.FontFamilyProperty, Roboto) }
            });

            // Buttons: Poppins 600, rounded.
            s.Add(new Style(x => x.OfType<Button>())
            {
                Setters =
                {
                    S(Button.FontFamilyProperty, Poppins),
                    S(Button.FontWeightProperty, FontWeight.SemiBold),
                    S(Button.FontSizeProperty, 13.0),
                    S(Button.CornerRadiusProperty, ButtonRadius),
                    S(Button.BorderThicknessProperty, new Thickness(1)),
                    S(Button.BackgroundProperty, WhiteBrush),
                    S(Button.ForegroundProperty, NavyBrush),
                    S(Button.BorderBrushProperty, NavyBrush),
                    S(Control.FocusAdornerProperty, FocusRing(8)),
                }
            });

            // The main button (Power on): orange fill, Navy text.
            s.Add(new Style(x => x.OfType<Button>().Class("primary"))
            {
                Setters = { S(Button.BackgroundProperty, OrangeBrush), S(Button.BorderBrushProperty, OrangeBrush), S(Button.ForegroundProperty, NavyBrush), S(Button.FontSizeProperty, 14.0) }
            });
            foreach (var state in new[] { ":pointerover", ":pressed" })
                s.Add(new Style(x => Part<ContentPresenter>(x.OfType<Button>().Class("primary"), state, "PART_ContentPresenter"))
                {
                    Setters = { S(ContentPresenter.BackgroundProperty, OrangeHoverBrush), S(ContentPresenter.BorderBrushProperty, OrangeHoverBrush), S(ContentPresenter.ForegroundProperty, NavyBrush) }
                });

            // The colored remote-control keys keep their own color and label color while hovered or pressed.
            s.Add(new Style(x => x.OfType<Button>().Class("colorkey")) { Setters = { S(Button.FontSizeProperty, 14.0) } });
            foreach (var state in new[] { ":pointerover", ":pressed" })
                s.Add(new Style(x => Part<ContentPresenter>(x.OfType<Button>().Class("colorkey"), state, "PART_ContentPresenter"))
                {
                    Setters =
                    {
                        S(ContentPresenter.BackgroundProperty, new Binding("Background") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) }),
                        S(ContentPresenter.ForegroundProperty, new Binding("Foreground") { RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent) }),
                    }
                });

            // Tabs: Mist with a Muted border; the selected one is solid Navy with white bold text.
            s.Add(new Style(x => x.OfType<TabItem>())
            {
                Setters =
                {
                    S(TabItem.FontFamilyProperty, Poppins),
                    S(TabItem.FontWeightProperty, FontWeight.SemiBold),
                    S(TabItem.FontSizeProperty, 14.0),
                    S(TabItem.ForegroundProperty, NavyBrush),
                    S(TabItem.BackgroundProperty, MistBrush),
                    S(TabItem.BorderBrushProperty, MutedBrush),
                    S(TabItem.BorderThicknessProperty, new Thickness(1)),
                    S(TabItem.CornerRadiusProperty, TabRadius),
                    S(Layoutable.MarginProperty, new Thickness(0, 0, 2, 2)),
                    S(Control.FocusAdornerProperty, FocusRing(4)),
                }
            });
            s.Add(new Style(x => x.OfType<TabItem>().Class(":selected"))
            {
                Setters =
                {
                    S(TabItem.ForegroundProperty, WhiteBrush),
                    S(TabItem.BackgroundProperty, NavyBrush),
                    S(TabItem.BorderBrushProperty, NavyBrush),
                    S(TabItem.BorderThicknessProperty, new Thickness(2)),
                    S(TabItem.FontWeightProperty, FontWeight.Bold),
                }
            });
            s.Add(new Style(x => x.OfType<TabItem>().Class(":selected").Class(":pointerover"))
            {
                Setters = { S(TabItem.BackgroundProperty, NavyDarkBrush), S(TabItem.ForegroundProperty, WhiteBrush) }
            });
            s.Add(new Style(x => Part<Border>(x.OfType<TabItem>().Class(":selected"), ":pointerover", "PART_LayoutRoot"))
            {
                Setters = { S(Border.BackgroundProperty, NavyDarkBrush) }
            });

            // Fields.
            s.Add(new Style(x => x.OfType<TextBox>())
            {
                Setters = { S(TextBox.CornerRadiusProperty, FieldRadius), S(TextBox.ForegroundProperty, InkBrush) }
            });
            s.Add(new Style(x => x.OfType<ComboBox>())
            {
                Setters = { S(ComboBox.CornerRadiusProperty, FieldRadius), S(ComboBox.ForegroundProperty, InkBrush) }
            });
        }
    }
}
