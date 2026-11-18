namespace CrestApps.OrchardCore.AdminTheme.Models;

/// <summary>
/// The site settings of the CrestApps admin theme.
/// </summary>
public sealed class AdminThemeSettings
{
    /// <summary>
    /// The id of the color scheme of the admin for the users who have not picked their own. The built-in scheme when
    /// empty or unknown. See <see cref="AdminColorSchemes"/>.
    /// </summary>
    public string DefaultColorScheme { get; set; }
}
