using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.AdminTheme.Models;

/// <summary>
/// The color schemes of the admin. The theme seeds its default schemes once (Gray, Blue, Ocean...); admins edit,
/// delete and add schemes from there. Kept in one document behind Orchard Core's cached <c>IDocumentManager</c>: read on
/// every admin page, written only when a scheme is saved or deleted.
/// </summary>
public sealed class AdminColorSchemesDocument : Document
{
    /// <summary>
    /// Gets or sets the schemes, by id.
    /// </summary>
    public Dictionary<string, AdminColorScheme> Schemes { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// A color scheme: a name and four colors, from which every token is derived, light and dark, with the contrast
/// WCAG 2.2 AA asks for (see <c>AdminColorSchemeCssBuilder</c>). The optional settings fine tune what is derived; left
/// empty, they are worked out from the four colors.
/// </summary>
public sealed class AdminColorScheme
{
    /// <summary>
    /// The id: the value of the page's 'data-ca-scheme' attribute. Lowercase letters, digits and dashes.
    /// </summary>
    public string Id { get; set; }

    public string Name { get; set; }

    /// <summary>
    /// The place of the scheme in the pickers, lowest first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Actions, links and the current page, as '#rrggbb'.
    /// </summary>
    public string Primary { get; set; }

    /// <summary>
    /// Headings and the top of the menu, as '#rrggbb'.
    /// </summary>
    public string Deep { get; set; }

    /// <summary>
    /// The bottom of the menu, the darkest surface, as '#rrggbb'.
    /// </summary>
    public string Ink { get; set; }

    /// <summary>
    /// Links and highlights on dark surfaces, as '#rrggbb'.
    /// </summary>
    public string Accent { get; set; }

    /// <summary>
    /// Whether the menu is light gray with dark text, like TheAdmin's, rather than dark on the scheme's colors.
    /// </summary>
    public bool LightMenu { get; set; }

    /// <summary>
    /// The hue of the page, borders and dark mode surfaces, 0 to 360; the deep color's when empty.
    /// </summary>
    public double? Hue { get; set; }

    /// <summary>
    /// How saturated the page, borders and dark mode surfaces are, 0 (gray) to 1; from the deep color when empty.
    /// </summary>
    public double? Saturation { get; set; }

    /// <summary>
    /// The color of text on the primary, as '#rrggbb'; white, or dark on a light primary, when empty.
    /// </summary>
    public string OnPrimary { get; set; }

    /// <summary>
    /// The page in light mode, as '#rrggbb'; a pale tone of the hue when empty.
    /// </summary>
    public string Page { get; set; }

    /// <summary>
    /// The toolbar of lists, as '#rrggbb'; the primary when empty.
    /// </summary>
    public string Toolbar { get; set; }

    /// <summary>
    /// The color of both ends of the page gradient in light mode, as '#rrggbb'; the primary and the accent when empty.
    /// </summary>
    public string Tint { get; set; }

    /// <summary>
    /// The color of both ends of the page gradient in dark mode, as '#rrggbb'; the accent and the primary when empty.
    /// </summary>
    public string TintDark { get; set; }
}
