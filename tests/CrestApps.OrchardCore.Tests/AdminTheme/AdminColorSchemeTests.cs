using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.AdminTheme;
using CrestApps.OrchardCore.AdminTheme.ColorSchemes;
using CrestApps.OrchardCore.AdminTheme.Models;
using CrestApps.OrchardCore.AdminTheme.Services;

namespace CrestApps.OrchardCore.Tests.AdminTheme;

/// <summary>
/// The admin theme derives the tokens of its built-in schemes in Sass (Assets/scss/_schemes.scss) and those of the
/// schemes admins add in C# (<see cref="AdminColorSchemeCssBuilder"/>). These tests keep both derivations in step, and
/// check that whatever colors an admin picks, the derived tokens meet WCAG 2.2 AA contrast.
/// </summary>
public sealed class AdminColorSchemeTests
{
    // The definitions the theme's schemes were tuned with; the seeded records must reproduce them exactly.
    public static TheoryData<string> DefaultSchemes => ["gray", "blue", "ocean", "forest", "plum", "sunset", "graphite", "gold"];

    private static readonly Dictionary<string, AdminColorSchemeDefinition> _builtIn = new(StringComparer.Ordinal)
    {
        ["gray"] = new()
        {
            Primary = Color("#0d6efd"), Deep = Color("#212529"), Ink = Color("#212529"), Accent = Color("#6ea8fe"),
            Hue = 210, Saturation = 0.12, LightMenu = true, Page = Color("#fff"),
            Tint = Color("#495057"), TintDark = Color("#adb5bd"), Toolbar = Color("#495057"),
        },
        ["blue"] = new() { Primary = Color("#2a81bb"), Deep = Color("#144c71"), Ink = Color("#081b26"), Accent = Color("#5aa4ef"), Hue = 209, Saturation = 1 },
        ["ocean"] = new() { Primary = Color("#12868b"), Deep = Color("#0d4a52"), Ink = Color("#062227"), Accent = Color("#4fd1c5"), Hue = 187, Saturation = 0.9 },
        ["forest"] = new() { Primary = Color("#2f855a"), Deep = Color("#1c4a36"), Ink = Color("#0b2018"), Accent = Color("#68d391"), Hue = 150, Saturation = 0.7 },
        ["plum"] = new() { Primary = Color("#7b4fc0"), Deep = Color("#3d2a66"), Ink = Color("#170f2b"), Accent = Color("#b794f4"), Hue = 262, Saturation = 0.7 },
        ["sunset"] = new() { Primary = Color("#c2551f"), Deep = Color("#5c2a1c"), Ink = Color("#24100b"), Accent = Color("#f6ad55"), Hue = 18, Saturation = 0.6 },
        ["graphite"] = new() { Primary = Color("#4b6584"), Deep = Color("#2d3540"), Ink = Color("#121519"), Accent = Color("#94a8bf"), Hue = 213, Saturation = 0.2 },
        ["gold"] = new()
        {
            Primary = Color("#eaa52c"), Deep = Color("#1e1e1e"), Ink = Color("#09080b"), Accent = Color("#eaa52c"),
            Hue = 240, Saturation = 0.08, OnPrimary = Color("#1e1e1e"),
        },
    };

    private static readonly Regex _colorRegex = new(@"#[0-9a-fA-F]{3,8}\b|rgba?\([^)]*\)", RegexOptions.Compiled);
    private static readonly Regex _numberRegex = new(@"-?\d+(\.\d+)?", RegexOptions.Compiled);

    // The stylesheet's fallback (Gray) is derived in Sass; every scheme record in C#. Both must agree.
    [Fact]
    public void CSharpDerivation_MatchesTheSassOne_InLightMode()
        => AssertSameTokens("gray", ReadBlock(":root", mustContain: "--ca-primary:"), AdminColorSchemeCssBuilder.BuildLight(_builtIn["gray"]));

    [Fact]
    public void CSharpDerivation_MatchesTheSassOne_InDarkMode()
        => AssertSameTokens("gray", ReadBlock(":root[data-bs-theme=dark]", mustContain: "--ca-link:"), AdminColorSchemeCssBuilder.BuildDark(_builtIn["gray"]));

    [Theory]
    [MemberData(nameof(DefaultSchemes))]
    public void SeededDefaults_ReproduceTheSchemesTheyWereTunedAs(string id)
    {
        var seed = AdminColorSchemes.CreateDefaults().Single(scheme => scheme.Id == id);
        var definition = AdminColorSchemeStore.ToDefinition(seed);

        Assert.NotNull(definition);
        Assert.Equal(AdminColorSchemeCssBuilder.BuildLight(_builtIn[id]), AdminColorSchemeCssBuilder.BuildLight(definition));
        Assert.Equal(AdminColorSchemeCssBuilder.BuildDark(_builtIn[id]), AdminColorSchemeCssBuilder.BuildDark(definition));
    }

    [Fact]
    public void SeededDefaults_HaveValidUniqueIds_AndAnOrder()
    {
        var defaults = AdminColorSchemes.CreateDefaults();

        Assert.All(defaults, scheme => Assert.True(AdminColorSchemeStore.IsValidId(scheme.Id), scheme.Id));
        Assert.Equal(defaults.Count, defaults.Select(scheme => scheme.Id).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(defaults.OrderBy(scheme => scheme.Position).Select(scheme => scheme.Id), defaults.Select(scheme => scheme.Id));
        Assert.Contains(defaults, scheme => scheme.Id == AdminColorSchemes.DefaultId);
    }

    [Theory]
    [InlineData("#ffd400", "#3a2f00", "#120f00", "#ffe680")] // A bright yellow primary, which white text cannot sit on.
    [InlineData("#f0f4ff", "#1f2a44", "#0b1020", "#9fb4ff")] // A primary almost as pale as the page.
    [InlineData("#5b0f2a", "#2b0614", "#12020a", "#ff8fb8")] // A very dark primary.
    [InlineData("#00ff66", "#00331a", "#001208", "#66ffaa")] // A neon one.
    public void AnySchemeAnAdminPicks_MeetsWcagContrast(string primary, string deep, string ink, string accent)
    {
        foreach (var lightMenu in new[] { false, true })
        {
            var scheme = AdminColorSchemeDefinition.FromColors(Color(primary), Color(deep), Color(ink), Color(accent), lightMenu);
            var light = ToDictionary(AdminColorSchemeCssBuilder.BuildLight(scheme));
            var dark = ToDictionary(AdminColorSchemeCssBuilder.BuildDark(scheme));

            foreach (var (mode, tokens) in new[] { ("light", light), ("dark", Merge(light, dark)) })
            {
                var surfaces = new[] { "--ca-page-bg", "--ca-surface", "--ca-surface-muted", "--ca-list-header-bg" }
                    .Select(name => Color(tokens[name]))
                    .ToArray();

                AssertContrast($"{mode} link", Color(tokens["--ca-link"]), surfaces, 4.5);
                AssertContrast($"{mode} muted text", Color(tokens["--ca-muted"]), surfaces, 4.5);
                AssertContrast($"{mode} control border", Color(tokens["--ca-control-border"]), surfaces, 3);
                AssertContrast($"{mode} focus ring", Color(tokens["--ca-focus-color"]), surfaces, 3);
                AssertContrast($"{mode} text on primary", Color(tokens["--ca-on-primary"]), [Color(tokens["--ca-primary"])], 4.5);
                AssertContrast($"{mode} text on hovered primary", Color(tokens["--ca-on-primary"]), [Color(tokens["--ca-primary-hover"])], 4.5);
                AssertContrast($"{mode} text on pressed primary", Color(tokens["--ca-on-primary"]), [Color(tokens["--ca-primary-active"])], 4.5);
                AssertContrast($"{mode} text on the toolbar", Color(tokens["--ca-toolbar-fg"]), [Color(tokens["--ca-toolbar-bg"])], 4.5);
            }
        }
    }

    [Fact]
    public void FromColors_PutsDarkTextOnALightPrimary()
    {
        var gold = AdminColorSchemeDefinition.FromColors(Color("#eaa52c"), Color("#1e1e1e"), Color("#09080b"), Color("#eaa52c"), lightMenu: false);
        var blue = AdminColorSchemeDefinition.FromColors(Color("#2a81bb"), Color("#144c71"), Color("#081b26"), Color("#5aa4ef"), lightMenu: false);

        Assert.NotNull(gold.OnPrimary);
        Assert.True(gold.OnPrimary.Value.Luminance < 0.1);
        Assert.Null(blue.OnPrimary);
    }

    [Theory]
    [InlineData("custom-4c3hkg22jkk551r5mg8wevt6cc", true)]
    [InlineData("custom-a-b-1", true)]
    [InlineData("blue", true)]
    [InlineData("1blue", false)]
    [InlineData("-blue", false)]
    [InlineData("custom-ABC", false)]
    [InlineData("custom-x\"]{color:red}", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void AddedSchemeIds_AreSafeCssValues(string id, bool valid)
        => Assert.Equal(valid, AdminColorSchemeStore.IsValidId(id));

    [Fact]
    public void Stylesheet_HoldsEveryValidScheme_AndSkipsTheRest()
    {
        var css = AdminColorSchemeStore.BuildStylesheet(
        [
            new AdminColorScheme { Id = "custom-ok", Name = "Ok", Primary = "#2a81bb", Deep = "#144c71", Ink = "#081b26", Accent = "#5aa4ef" },
            new AdminColorScheme { Id = "custom-bad-color", Name = "Bad", Primary = "blue", Deep = "#144c71", Ink = "#081b26", Accent = "#5aa4ef" },
            new AdminColorScheme { Id = "Bad Id", Name = "Bad id", Primary = "#2a81bb", Deep = "#144c71", Ink = "#081b26", Accent = "#5aa4ef" },
        ]);

        Assert.Contains("[data-ca-scheme=\"custom-ok\"]{", css, StringComparison.Ordinal);
        Assert.Contains("[data-bs-theme=\"dark\"][data-ca-scheme=\"custom-ok\"]{", css, StringComparison.Ordinal);
        Assert.Contains(".ca-scheme-swatch[data-scheme=\"custom-ok\"]", css, StringComparison.Ordinal);
        Assert.DoesNotContain("custom-bad-color", css, StringComparison.Ordinal);
        Assert.DoesNotContain("Bad Id", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Contrast_FollowsWcag()
    {
        Assert.Equal(21, AdminColor.White.ContrastWith(AdminColor.Black), precision: 6);
        Assert.Equal(1, AdminColor.White.ContrastWith(AdminColor.White), precision: 6);
        Assert.Equal(4.54, Color("#767676").ContrastWith(AdminColor.White), precision: 2);
    }

    private static void AssertSameTokens(string scheme, Dictionary<string, string> sass, IReadOnlyList<KeyValuePair<string, string>> csharp)
    {
        var generated = ToDictionary(csharp);

        Assert.Equal(sass.Keys.Order(StringComparer.Ordinal), generated.Keys.Order(StringComparer.Ordinal));

        foreach (var (name, expected) in sass)
        {
            var expectedNumbers = Numbers(expected);
            var actualNumbers = Numbers(generated[name]);

            Assert.True(expectedNumbers.Count == actualNumbers.Count, $"{scheme} {name}: Sass '{expected}', C# '{generated[name]}'.");

            for (var i = 0; i < expectedNumbers.Count; i++)
            {
                // A channel may round one step apart; Sass keeps fractions, C# writes rounded hex.
                Assert.True(Math.Abs(expectedNumbers[i] - actualNumbers[i]) <= 1.01, $"{scheme} {name}: Sass '{expected}', C# '{generated[name]}'.");
            }
        }
    }

    private static void AssertContrast(string what, AdminColor color, IEnumerable<AdminColor> backgrounds, double minimum)
    {
        var lowest = color.MinContrast(backgrounds);

        Assert.True(lowest >= minimum, $"{what}: {lowest:0.00} is under {minimum}.");
    }

    // The numbers of a token, colors as their 0-255 channels, so '#2677ae', 'rgb(14.9%, 46.7%, 68.2%)' and
    // '%232677ae' (a color in an SVG data URI) compare alike.
    private static List<double> Numbers(string value)
    {
        value = value.Replace("%23", "#", StringComparison.Ordinal);
        var numbers = new List<double>();
        var position = 0;

        foreach (Match match in _colorRegex.Matches(value))
        {
            AddPlainNumbers(numbers, value[position..match.Index]);
            var color = ParseCssColor(match.Value, out var alpha);
            numbers.AddRange([color.R, color.G, color.B]);

            if (alpha is not null)
            {
                numbers.Add(alpha.Value * 100);
            }

            position = match.Index + match.Length;
        }

        AddPlainNumbers(numbers, value[position..]);

        return numbers;
    }

    private static void AddPlainNumbers(List<double> numbers, string text)
    {
        // Keep the triplets of the '-rgb' tokens and the stops of gradients; skip the numbers of the SVG markup.
        if (text.Contains("svg", StringComparison.Ordinal))
        {
            return;
        }

        foreach (Match match in _numberRegex.Matches(text))
        {
            numbers.Add(double.Parse(match.Value, CultureInfo.InvariantCulture));
        }
    }

    private static AdminColor ParseCssColor(string value, out double? alpha)
    {
        alpha = null;

        if (value.StartsWith('#'))
        {
            return Color(value.Length == 9 ? value[..7] : value);
        }

        var parts = value[(value.IndexOf('(') + 1)..^1].Split(',', StringSplitOptions.TrimEntries);
        var channels = parts.Take(3)
            .Select(part => part.EndsWith('%')
                ? double.Parse(part[..^1], CultureInfo.InvariantCulture) * 2.55
                : double.Parse(part, CultureInfo.InvariantCulture))
            .ToArray();

        if (parts.Length > 3)
        {
            alpha = double.Parse(parts[3], CultureInfo.InvariantCulture);
        }

        return new AdminColor(channels[0], channels[1], channels[2]);
    }

    private static Dictionary<string, string> ReadBlock(string selector, string mustContain)
    {
        var css = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Themes", "CrestApps.OrchardCore.AdminTheme", "wwwroot", "styles", "admin-theme.css"));
        var start = 0;

        while ((start = css.IndexOf(selector + " {", start, StringComparison.Ordinal)) >= 0)
        {
            var open = css.IndexOf('{', start);
            var close = css.IndexOf('}', open);
            var body = css[(open + 1)..close];

            if (body.Contains(mustContain, StringComparison.Ordinal) && (start == 0 || css[start - 1] == '\n'))
            {
                return body.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(declaration => declaration.StartsWith("--", StringComparison.Ordinal))
                    .Select(declaration => declaration.Split(':', 2))
                    .ToDictionary(pair => pair[0].Trim(), pair => pair[1].Trim(), StringComparer.Ordinal);
            }

            start = close;
        }

        throw new InvalidOperationException($"No '{selector}' block with '{mustContain}' in admin-theme.css.");
    }

    private static Dictionary<string, string> ToDictionary(IReadOnlyList<KeyValuePair<string, string>> tokens)
        => tokens.ToDictionary(token => token.Key, token => token.Value, StringComparer.Ordinal);

    private static Dictionary<string, string> Merge(Dictionary<string, string> light, Dictionary<string, string> dark)
    {
        var merged = new Dictionary<string, string>(light, StringComparer.Ordinal);

        foreach (var (name, value) in dark)
        {
            merged[name] = value;
        }

        return merged;
    }

    private static AdminColor Color(string value) => AdminColor.Parse(value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests", "CrestApps.OrchardCore.Tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the repository root from the test assembly location.");
    }
}
