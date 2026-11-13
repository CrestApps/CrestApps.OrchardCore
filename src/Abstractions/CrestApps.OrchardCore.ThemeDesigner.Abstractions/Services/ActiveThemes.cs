namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// The themes the site and the admin use.
/// </summary>
public sealed class ActiveThemes
{
    /// <summary>
    /// Gets or sets the identifier of the front-end theme.
    /// </summary>
    public string SiteThemeId { get; set; }

    /// <summary>
    /// Gets or sets the name of the front-end theme.
    /// </summary>
    public string SiteThemeName { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the admin theme.
    /// </summary>
    public string AdminThemeId { get; set; }

    /// <summary>
    /// Gets or sets the name of the admin theme.
    /// </summary>
    public string AdminThemeName { get; set; }
}
