using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using SonyTvRemote;
using Xunit;

// 1.5.0: navy and orange theme. Nothing here opens a window or touches the network.

namespace SonyTvRemote.Tests
{
    public class Release150Tests
    {
        // WCAG 2.x: relative luminance and contrast ratio.
        private static double Channel(byte v)
        {
            double c = v / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        private static double Luminance(Color c) => 0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);

        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        [Fact]
        public void Contrast_formula_matches_the_known_extremes()
        {
            Assert.Equal(21.0, Contrast(Color.Parse("#000000"), Color.Parse("#FFFFFF")), 1);
            Assert.Equal(1.0, Contrast(Color.Parse("#777777"), Color.Parse("#777777")), 3);
        }

        public static TheoryData<string, string, string> TextPairs() => new TheoryData<string, string, string>
        {
            { "Navy on White", "Navy", "White" },
            { "Navy on Mist", "Navy", "Mist" },
            { "Navy on NavyLight", "Navy", "NavyLight" },
            { "White on Navy", "White", "Navy" },
            { "White on NavyDark", "White", "NavyDark" },
            { "Navy on Orange", "Navy", "Orange" },
            { "Navy on OrangeHover", "Navy", "OrangeHover" },
            { "Ink on White", "Ink", "White" },
            { "Muted on White", "Muted", "White" },
            { "Muted on Mist", "Muted", "Mist" },
            { "Success on White", "Success", "White" },
            { "Error on White", "Error", "White" },
            { "OrangeText on White", "OrangeText", "White" },
            { "Red key text", "KeyLightText", "KeyRed" },
            { "Green key text", "KeyLightText", "KeyGreen" },
            { "Yellow key text", "KeyDarkText", "KeyYellow" },
            { "Blue key text", "KeyLightText", "KeyBlue" },
            { "selected text", "White", "Navy" },
        };

        private static Color Token(string name)
            => (Color)typeof(AppTheme).GetField(name).GetValue(null);

        [Theory]
        [MemberData(nameof(TextPairs))]
        public void Text_contrast_is_at_least_4_5(string label, string text, string background)
        {
            double ratio = Contrast(Token(text), Token(background));
            Assert.True(ratio >= 4.5, label + ": " + ratio.ToString("0.00") + ":1");
        }

        public static TheoryData<string, string, string> BorderPairs() => new TheoryData<string, string, string>
        {
            { "button border", "Navy", "White" },
            { "field border", "Muted", "White" },
            { "tab border", "Muted", "Mist" },
        };

        [Theory]
        [MemberData(nameof(BorderPairs))]
        public void Control_border_contrast_is_at_least_3(string label, string border, string background)
        {
            double ratio = Contrast(Token(border), Token(background));
            Assert.True(ratio >= 3.0, label + ": " + ratio.ToString("0.00") + ":1");
        }

        // Selected text in the fields: Navy highlight with White text (the default kept Ink text on Navy, about 1:1).
        [Fact]
        public void The_text_box_style_sets_both_selection_colors()
        {
            var styles = new Styles();
            AppTheme.AddStyles(styles);
            var setters = styles.OfType<Style>()
                .Where(s => s.Selector != null && s.Selector.ToString() == "TextBox")
                .SelectMany(s => s.Setters.OfType<Setter>())
                .ToList();

            var highlight = setters.Single(x => x.Property == TextBox.SelectionBrushProperty).Value;
            var text = setters.Single(x => x.Property == TextBox.SelectionForegroundBrushProperty).Value;
            Assert.Equal(AppTheme.Navy, Assert.IsAssignableFrom<ISolidColorBrush>(highlight).Color);
            Assert.Equal(AppTheme.White, Assert.IsAssignableFrom<ISolidColorBrush>(text).Color);
        }

        [Fact]
        public void The_theme_values_are_the_specified_ones()
        {
            Assert.Equal(Color.Parse("#131B59"), AppTheme.Navy);
            Assert.Equal(Color.Parse("#FF9B00"), AppTheme.Orange);
            Assert.Equal(Color.Parse("#1E7A46"), AppTheme.KeyGreen);
            Assert.Equal(Color.Parse("#1E7A46"), AppTheme.Success);
        }

        // ---------- fonts ----------

        private static string FontsDir()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "app", "Assets", "Fonts");
                if (Directory.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException("app/Assets/Fonts");
        }

        public static TheoryData<string, string> FontFiles() => new TheoryData<string, string>
        {
            { "Poppins-SemiBold.ttf", "d3bf1bdaf0550e83da9ac0b1d1d9fe6db086835a83aa28578e609a394b9a0286" },
            { "Roboto-Variable.ttf", "d7598e12c5dbef095ff8272cfc55da0250bd07fbdecbac8a530b9b277872a134" },
            { "Poppins-OFL.txt", "6be04893d770899a015649c7aa3b582f871b272f8747a92b78b17c3e5c8b2573" },
            { "Roboto-OFL.txt", "061402327a96aadb0bfb694a960ed289ecd38d383e396243831ab81feb109c41" },
            // The weight 400, width 100 instance of Roboto-Variable.ttf that the .exe embeds.
            { "Roboto-Regular.ttf", "9b4fc24bff70005d71013e158110a75920533e25413c41759a0f9b1cdf11c546" },
        };

        [Theory]
        [MemberData(nameof(FontFiles))]
        public void Font_file_exists_with_the_expected_sha256(string file, string sha256)
        {
            var path = Path.Combine(FontsDir(), file);
            Assert.True(File.Exists(path), file);
            using (var stream = File.OpenRead(path))
                Assert.Equal(sha256, BitConverter.ToString(SHA256.Create().ComputeHash(stream)).Replace("-", "").ToLowerInvariant());
        }

        // ---------- version ----------

        [Fact]
        public void The_version_is_1_5_0() => Assert.Equal("1.5.0", AppInfo.Version);

        [Theory]
        [InlineData(Lang.EnUs)]
        [InlineData(Lang.PtBr)]
        public void The_1_5_0_history_entry_exists_in_both_languages(Lang lang)
        {
            var text = Strings.Get(lang, "ChangelogText");
            Assert.StartsWith("1.5.0 ", text);
            Assert.Contains("1.4.0", text);
        }
    }
}
