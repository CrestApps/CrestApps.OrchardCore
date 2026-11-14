namespace CrestApps.OrchardCore.AdminTheme.ColorSchemes;

/// <summary>
/// What a color scheme is made of: four colors and a hue, as in the '$schemes' map of Assets/scss/_schemes.scss.
/// <list type="bullet">
/// <item><see cref="Primary"/>: actions, links and the current page;</item>
/// <item><see cref="Deep"/>: headings and the top of the menu;</item>
/// <item><see cref="Ink"/>: the bottom of the menu, the darkest surface;</item>
/// <item><see cref="Accent"/>: links and highlights on dark surfaces;</item>
/// <item><see cref="Hue"/> and <see cref="Saturation"/>: the tint of the page, the borders and the dark mode surfaces.</item>
/// </list>
/// </summary>
public sealed class AdminColorSchemeDefinition
{
    public AdminColor Primary { get; init; }

    public AdminColor Deep { get; init; }

    public AdminColor Ink { get; init; }

    public AdminColor Accent { get; init; }

    /// <summary>
    /// The hue of the scheme's surfaces, 0 to 360.
    /// </summary>
    public double Hue { get; init; }

    /// <summary>
    /// How saturated the scheme's surfaces are, 0 (gray) to 1.
    /// </summary>
    public double Saturation { get; init; } = 1;

    /// <summary>
    /// Whether the menu is light gray with dark text, as in TheAdmin, rather than dark on the scheme's colors.
    /// </summary>
    public bool LightMenu { get; init; }

    /// <summary>
    /// The color of text and marks drawn on the primary; white when not set.
    /// </summary>
    public AdminColor? OnPrimary { get; init; }

    /// <summary>
    /// The page color in light mode; a pale tone of the hue when not set.
    /// </summary>
    public AdminColor? Page { get; init; }

    /// <summary>
    /// The color of both ends of the page gradient in light mode; the primary and the accent when not set.
    /// </summary>
    public AdminColor? Tint { get; init; }

    /// <summary>
    /// The color of both ends of the page gradient in dark mode; the accent and the primary when not set.
    /// </summary>
    public AdminColor? TintDark { get; init; }

    /// <summary>
    /// The color of the toolbar of lists; the primary when not set.
    /// </summary>
    public AdminColor? Toolbar { get; init; }

    /// <summary>
    /// Builds the definition of a scheme from its four colors only, working out the rest the way the built-in schemes
    /// were tuned: the hue and saturation from the deep color, and dark text on the primary when white text would need
    /// the primary darkened too far (a light primary such as a gold).
    /// </summary>
    public static AdminColorSchemeDefinition FromColors(AdminColor primary, AdminColor deep, AdminColor ink, AdminColor accent, bool lightMenu)
    {
        var (hue, saturation) = deep.ToHueSaturation();
        AdminColor? onPrimary = null;

        // White text that would only pass on a much darker primary: the primary keeps its color, the text turns dark.
        if (primary.ContrastWith(AdminColor.White) < 3)
        {
            var dark = ink.ContrastWith(primary) >= AdminColorSchemeCssBuilder.TextContrast ? ink : new AdminColor(17, 17, 17);
            onPrimary = dark;
        }

        return new AdminColorSchemeDefinition
        {
            Primary = primary,
            Deep = deep,
            Ink = ink,
            Accent = accent,
            Hue = hue,
            Saturation = Math.Clamp(saturation / 0.7, 0.08, 1),
            LightMenu = lightMenu,
            OnPrimary = onPrimary,
        };
    }
}
