namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A template that rendered part of a previewed page: the name a template must take to replace it, and where the one
/// used comes from.
/// </summary>
public sealed class ThemeDraftRenderedTemplate
{
    /// <summary>
    /// Gets or sets the shape type or alternate whose template was used, such as <c>Content__Blog</c>. A template with
    /// this name replaces it.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets where the template comes from: the theme's or a module's file (such as
    /// <c>Areas/TheBlogTheme/Views/Content-Blog.liquid</c>), <c>published template</c> or <c>theme draft</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the more specific names that would replace it for this content only, such as
    /// <c>Content__Alias__home</c>, most specific first.
    /// </summary>
    public IReadOnlyList<string> MoreSpecificNames { get; set; } = [];
}
