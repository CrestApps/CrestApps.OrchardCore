using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Tells a designer which themes are active, which shapes they render and with what templates, so a template can
/// start from the theme's own markup.
/// </summary>
public interface IThemeShapeExplorer
{
    /// <summary>
    /// Gets the front-end and admin themes.
    /// </summary>
    Task<ActiveThemes> GetActiveThemesAsync();

    /// <summary>
    /// Lists the shapes the active theme of a side of the site can render, matching <paramref name="search"/>.
    /// </summary>
    /// <param name="scope">The side of the site.</param>
    /// <param name="search">Text the shape type or its source must contain, or <see langword="null"/> for all.</param>
    /// <param name="take">The most shapes to return.</param>
    /// <returns>The shapes, and how many matched in all.</returns>
    Task<(IReadOnlyList<ThemeShapeInfo> Shapes, int Total)> ListShapesAsync(ThemeDesignScope scope, string search, int take);

    /// <summary>
    /// Gets the template the active theme renders a shape with, or <see langword="null"/> when the shape is unknown.
    /// </summary>
    /// <param name="scope">The side of the site.</param>
    /// <param name="shapeType">The shape type or alternate.</param>
    Task<ThemeShapeSource> GetShapeSourceAsync(ThemeDesignScope scope, string shapeType);
}
