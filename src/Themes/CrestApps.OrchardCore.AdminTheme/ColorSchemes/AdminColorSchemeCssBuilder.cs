using System.Text;

namespace CrestApps.OrchardCore.AdminTheme.ColorSchemes;

/// <summary>
/// Writes the CSS of a color scheme: its '--ca-*' tokens for the light and the dark mode, and the colors of its swatch.
/// It is the C# counterpart of the 'scheme-light', 'scheme-dark' and menu mixins of Assets/scss/_schemes.scss, which
/// build the built-in schemes; a parity test keeps both in step.
/// </summary>
public static class AdminColorSchemeCssBuilder
{
    // WCAG 2.2 AA with a small margin for rounding: 4.5:1 for text, 3:1 for the boundaries of controls and focus rings.
    public const double TextContrast = 4.6;

    public const double UiContrast = 3.1;

    private static readonly AdminColor _muted = AdminColor.Parse("#6d7384");
    private static readonly AdminColor _lightMenu = AdminColor.Parse("#f1f3f5");
    private static readonly AdminColor _lightMenuText = AdminColor.Parse("#495057");

    /// <summary>
    /// Appends the rules of the scheme <paramref name="id"/> to <paramref name="css"/>. The id must be a CSS identifier
    /// (letters, digits and dashes); the caller validates it.
    /// </summary>
    public static void Append(StringBuilder css, string id, AdminColorSchemeDefinition scheme)
    {
        css.Append("[data-ca-scheme=\"").Append(id).Append("\"]{");
        AppendTokens(css, BuildLight(scheme));
        css.Append("}\n");

        css.Append("[data-bs-theme=\"dark\"][data-ca-scheme=\"").Append(id).Append("\"]{");
        AppendTokens(css, BuildDark(scheme));
        css.Append("}\n");

        // The swatch shows the menu first: its dark end and its top, or the light gray of a light menu.
        var swatch = scheme.LightMenu
            ? new[] { _lightMenu, AdminColor.Parse("#dee2e6"), scheme.Primary, scheme.Deep }
            : new[] { scheme.Ink, scheme.Deep, scheme.Primary, scheme.Accent };

        for (var i = 0; i < swatch.Length; i++)
        {
            css.Append(".ca-scheme-swatch[data-scheme=\"").Append(id).Append("\"]>span:nth-child(").Append(i + 1)
                .Append("){background-color:").Append(swatch[i].ToHex()).Append("}\n");
        }
    }

    /// <summary>
    /// The tokens of the light mode, and the ones both modes share, in the order the Sass mixin writes them.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> BuildLight(AdminColorSchemeDefinition scheme)
    {
        var tokens = new List<KeyValuePair<string, string>>();
        var on = scheme.OnPrimary ?? AdminColor.White;
        var deep = scheme.Deep;
        var ink = scheme.Ink;
        var primary = scheme.Primary.EnsureContrast([on], TextContrast, ink);
        var accent = scheme.Accent;
        var hue = scheme.Hue;
        var k = scheme.Saturation;
        var page = scheme.Page ?? AdminColor.FromHsl(hue, 0.5 * k, 0.97);
        var surfaceMuted = AdminColor.FromHsl(hue, 0.6 * k, 0.982);
        var tintStart = (scheme.Tint ?? primary).Mix(page, 0.07);
        var tintEnd = (scheme.Tint ?? accent).Mix(page, 0.09);
        var toolbar = (scheme.Toolbar ?? primary).EnsureContrast([on], TextContrast, ink);
        var listHeader = AdminColor.FromHsl(hue, 0.14 * k, 0.975);
        var rowHover = primary.Mix(AdminColor.White, 0.08);
        AdminColor[] surfaces = [page, AdminColor.White, surfaceMuted, tintStart, tintEnd, listHeader, rowHover];
        var border = AdminColor.FromHsl(hue, 0.38 * k, 0.92);
        var menuMiddle = deep.Mix(ink, 0.5);
        var link = primary.EnsureContrast(surfaces, TextContrast, ink);
        var lightText = on.Luminance > 0.5;

        Add(tokens, "--ca-primary", primary);
        tokens.Add(new("--ca-primary-rgb", primary.ToTriplet()));
        Add(tokens, "--ca-primary-hover", Press(primary, lightText, deep, 0.22));
        Add(tokens, "--ca-primary-active", Press(primary, lightText, deep, 0.34));
        Add(tokens, "--ca-primary-subtle", primary.Mix(AdminColor.White, 0.2));
        Add(tokens, "--ca-primary-border-subtle", primary.Mix(AdminColor.White, 0.4));
        Add(tokens, "--ca-primary-emphasis", primary.Mix(AdminColor.Black, 0.4));
        Add(tokens, "--ca-navy", deep);
        tokens.Add(new("--ca-navy-rgb", deep.ToTriplet()));
        Add(tokens, "--ca-ink", ink);
        Add(tokens, "--ca-sky", accent);
        tokens.Add(new("--ca-sky-rgb", accent.ToTriplet()));
        Add(tokens, "--ca-tint", primary.Mix(AdminColor.White, 0.09));
        Add(tokens, "--ca-link", link);
        tokens.Add(new("--ca-link-rgb", link.ToTriplet()));
        Add(tokens, "--ca-focus-color", link);
        Add(tokens, "--ca-link-hover", deep);
        tokens.Add(new("--ca-link-hover-rgb", deep.ToTriplet()));
        Add(tokens, "--ca-page-bg", page);
        tokens.Add(new("--ca-surface", "#ffffff"));
        tokens.Add(new("--ca-surface-rgb", "255, 255, 255"));
        Add(tokens, "--ca-surface-muted", surfaceMuted);
        Add(tokens, "--ca-toolbar-bg", toolbar);
        Add(tokens, "--ca-toolbar-fg", on);
        tokens.Add(new("--ca-toolbar-fg-rgb", on.ToTriplet()));
        tokens.Add(new("--ca-toolbar-hover-bg", lightText ? AdminColor.Black.ToRgba(0.16) : AdminColor.White.ToRgba(0.3)));
        Add(tokens, "--ca-list-header-bg", listHeader);
        Add(tokens, "--ca-row-hover-bg", rowHover);
        Add(tokens, "--ca-border", border);
        Add(tokens, "--ca-control-border", border.EnsureContrast(surfaces, UiContrast, deep));
        Add(tokens, "--ca-control-checked-border", primary.EnsureContrast(surfaces, UiContrast, ink));
        AddOnPrimaryTokens(tokens, on);
        Add(tokens, "--ca-muted", _muted.EnsureContrast(surfaces, TextContrast, AdminColor.Black));
        Add(tokens, "--ca-heading", deep);
        Add(tokens, "--ca-label", AdminColor.FromHsl(hue, 0.32 * k, 0.35));
        Add(tokens, "--ca-topbar-color", AdminColor.FromHsl(hue, 0.28 * k, 0.28));
        tokens.Add(new("--ca-page-gradient", Gradient(135, (tintStart, 0), (page, 48), (tintEnd, 100))));

        if (scheme.LightMenu)
        {
            AddLightMenu(tokens, primary);
        }
        else
        {
            AddDarkMenu(tokens, scheme, primary, on);
            Add(tokens, "--ca-menu-bg", menuMiddle);
            tokens.Add(new("--ca-menu-gradient", Gradient(180, (deep, 0), (menuMiddle, 48), (ink, 100))));
        }

        return tokens;
    }

    /// <summary>
    /// The tokens of the dark mode, in the order the Sass mixin writes them.
    /// </summary>
    public static IReadOnlyList<KeyValuePair<string, string>> BuildDark(AdminColorSchemeDefinition scheme)
    {
        var tokens = new List<KeyValuePair<string, string>>();
        var on = scheme.OnPrimary ?? AdminColor.White;
        var ink = scheme.Ink;
        var primary = scheme.Primary.EnsureContrast([on], TextContrast, ink);
        var accent = scheme.Accent;
        var hue = scheme.Hue;
        var k = scheme.Saturation;
        var page = AdminColor.FromHsl(hue, 0.57 * k, 0.135);
        var surface = AdminColor.FromHsl(hue, 0.57 * k, 0.185);
        var surfaceMuted = AdminColor.FromHsl(hue, 0.56 * k, 0.216);
        var tintStart = (scheme.TintDark ?? accent).Mix(page, 0.08);
        var tintEnd = (scheme.TintDark ?? primary).Mix(page, 0.12);
        var listHeader = AdminColor.FromHsl(hue, 0.5 * k, 0.21);
        var rowHover = primary.Mix(surface, 0.16);
        AdminColor[] surfaces = [page, surface, surfaceMuted, tintStart, tintEnd, listHeader, rowHover];
        var border = AdminColor.FromHsl(hue, 0.49 * k, 0.28);
        var bodyColor = AdminColor.FromHsl(hue, 0.35 * k, 0.92);
        var link = accent.EnsureContrast(surfaces, TextContrast, AdminColor.White);
        var lightText = on.Luminance > 0.5;
        var subtleBase = AdminColor.FromHsl(hue, 0.57 * k, 0.12);

        Add(tokens, "--ca-primary-hover", Press(primary, lightText, ink, 0.14));
        Add(tokens, "--ca-primary-active", Press(primary, lightText, ink, 0.24));
        Add(tokens, "--ca-primary-subtle", primary.Mix(subtleBase, 0.22));
        Add(tokens, "--ca-primary-border-subtle", primary.Mix(subtleBase, 0.5));
        Add(tokens, "--ca-primary-emphasis", accent.Mix(AdminColor.White, 0.6));
        Add(tokens, "--ca-link", link);
        tokens.Add(new("--ca-link-rgb", link.ToTriplet()));
        Add(tokens, "--ca-link-hover", link.Mix(AdminColor.White, 0.7));
        tokens.Add(new("--ca-link-hover-rgb", link.Mix(AdminColor.White, 0.7).ToTriplet()));
        Add(tokens, "--ca-focus-color", link);
        Add(tokens, "--ca-heading", AdminColor.FromHsl(hue, 0.45 * k, 0.94));
        Add(tokens, "--ca-page-bg", page);
        Add(tokens, "--ca-surface", surface);
        tokens.Add(new("--ca-surface-rgb", surface.ToTriplet()));
        Add(tokens, "--ca-surface-muted", surfaceMuted);
        Add(tokens, "--ca-list-header-bg", listHeader);
        Add(tokens, "--ca-row-hover-bg", rowHover);
        Add(tokens, "--ca-secondary-bg", AdminColor.FromHsl(hue, 0.55 * k, 0.233));
        Add(tokens, "--ca-border", border);
        Add(tokens, "--ca-control-border", border.EnsureContrast(surfaces, UiContrast, AdminColor.White));
        Add(tokens, "--ca-control-checked-border", accent.EnsureContrast(surfaces, UiContrast, AdminColor.White));
        Add(tokens, "--ca-muted", AdminColor.FromHsl(hue, 0.29 * k, 0.70).EnsureContrast(surfaces, TextContrast, AdminColor.White));
        Add(tokens, "--ca-label", AdminColor.FromHsl(hue, 0.33 * k, 0.79));
        Add(tokens, "--ca-body-color", bodyColor);
        tokens.Add(new("--ca-body-color-rgb", bodyColor.ToTriplet()));
        Add(tokens, "--ca-topbar-bg", AdminColor.FromHsl(hue, 0.56 * k, 0.17));
        Add(tokens, "--ca-topbar-color", AdminColor.FromHsl(hue, 0.30 * k, 0.83));
        Add(tokens, "--ca-menu-bg", AdminColor.FromHsl(hue, 0.70 * k, 0.17));
        tokens.Add(new("--ca-menu-gradient", Gradient(
            180,
            (AdminColor.FromHsl(hue, 0.68 * k, 0.235), 0),
            (AdminColor.FromHsl(hue, 0.70 * k, 0.18), 55),
            (AdminColor.FromHsl(hue, 0.71 * k, 0.147), 100))));
        tokens.Add(new("--ca-page-gradient", Gradient(135, (tintStart, 0), (page, 48), (tintEnd, 100))));
        AddDarkMenu(tokens, scheme, primary, on);

        return tokens;
    }

    private static void AddDarkMenu(List<KeyValuePair<string, string>> tokens, AdminColorSchemeDefinition scheme, AdminColor primary, AdminColor on)
    {
        tokens.Add(new("--ca-menu-strong", "#fff"));
        tokens.Add(new("--ca-menu-strong-rgb", "255, 255, 255"));
        tokens.Add(new("--ca-menu-color", AdminColor.White.ToRgba(0.78)));
        tokens.Add(new("--ca-menu-muted", AdminColor.White.ToRgba(0.72)));
        tokens.Add(new("--ca-menu-hover-bg", AdminColor.White.ToRgba(0.08)));
        tokens.Add(new("--ca-menu-border", AdminColor.White.ToRgba(0.08)));
        Add(tokens, "--ca-menu-active-bg", primary);
        Add(tokens, "--ca-menu-active-color", on);
        Add(tokens, "--ca-menu-accent", scheme.Accent);
        tokens.Add(new("--ca-menu-accent-rgb", scheme.Accent.ToTriplet()));
    }

    private static void AddLightMenu(List<KeyValuePair<string, string>> tokens, AdminColor primary)
    {
        var accent = primary.EnsureContrast([_lightMenu], UiContrast, AdminColor.Black);

        tokens.Add(new("--ca-menu-strong", "#212529"));
        tokens.Add(new("--ca-menu-strong-rgb", "33, 37, 41"));
        tokens.Add(new("--ca-menu-color", "#495057"));
        tokens.Add(new("--ca-menu-muted", "#5c636a"));
        tokens.Add(new("--ca-menu-hover-bg", _lightMenuText.ToRgba(0.08)));
        tokens.Add(new("--ca-menu-border", "#dee2e6"));
        Add(tokens, "--ca-menu-active-bg", _lightMenuText.Mix(_lightMenu, 0.14));
        tokens.Add(new("--ca-menu-active-color", "#212529"));
        Add(tokens, "--ca-menu-accent", accent);
        tokens.Add(new("--ca-menu-accent-rgb", accent.ToTriplet()));
        Add(tokens, "--ca-menu-bg", _lightMenu);
        tokens.Add(new("--ca-menu-gradient", Gradient(180, (_lightMenu, 0), (_lightMenu, 100))));
    }

    private static void AddOnPrimaryTokens(List<KeyValuePair<string, string>> tokens, AdminColor on)
    {
        var c = "%23" + on.ToHex()[1..];

        Add(tokens, "--ca-on-primary", on);
        tokens.Add(new("--ca-check-icon", $"url(\"data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'%3e%3cpath fill='none' stroke='{c}' stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='m6 10 3 3 6-6'/%3e%3c/svg%3e\")"));
        tokens.Add(new("--ca-indeterminate-icon", $"url(\"data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 20 20'%3e%3cpath fill='none' stroke='{c}' stroke-linecap='round' stroke-linejoin='round' stroke-width='3' d='M6 10h8'/%3e%3c/svg%3e\")"));
        tokens.Add(new("--ca-radio-icon", $"url(\"data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='-4 -4 8 8'%3e%3ccircle r='2' fill='{c}'/%3e%3c/svg%3e\")"));
        tokens.Add(new("--ca-switch-icon", $"url(\"data:image/svg+xml,%3csvg xmlns='http://www.w3.org/2000/svg' viewBox='-4 -4 8 8'%3e%3ccircle r='3' fill='{c}'/%3e%3c/svg%3e\")"));
    }

    // Hovering and pressing move the primary away from its text color: darker under light text, lighter under dark.
    private static AdminColor Press(AdminColor primary, bool lightText, AdminColor towardDark, double amount)
        => lightText ? towardDark.Mix(primary, amount) : AdminColor.White.Mix(primary, amount);

    private static string Gradient(int angle, params (AdminColor Color, int Stop)[] stops)
        => $"linear-gradient({angle}deg, {string.Join(", ", stops.Select(stop => $"{stop.Color.ToHex()} {stop.Stop}%"))})";

    private static void Add(List<KeyValuePair<string, string>> tokens, string name, AdminColor color)
        => tokens.Add(new(name, color.ToHex()));

    private static void AppendTokens(StringBuilder css, IReadOnlyList<KeyValuePair<string, string>> tokens)
    {
        foreach (var token in tokens)
        {
            css.Append(token.Key).Append(':').Append(token.Value).Append(';');
        }
    }
}
