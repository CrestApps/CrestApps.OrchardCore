namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A stylesheet a previewed page loads.
/// </summary>
public sealed class ThemeDraftStylesheet
{
    /// <summary>
    /// Gets or sets the stylesheet's address.
    /// </summary>
    public string Url { get; set; }

    /// <summary>
    /// Gets or sets the stylesheet's size in bytes, or <see langword="null"/> when it isn't served by this site.
    /// </summary>
    public long? Bytes { get; set; }

    /// <summary>
    /// Gets or sets whether the stylesheet is one of the site's own resources (published or in the draft), rather than
    /// the theme's, a module's or another site's.
    /// </summary>
    public bool IsSiteResource { get; set; }
}
