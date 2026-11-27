namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A page of the site as the signed-in designer sees it with their theme draft.
/// </summary>
public sealed class ThemeDraftPagePreview
{
    /// <summary>
    /// Gets or sets the path that was rendered, such as <c>/</c> or <c>/blog/my-post</c>.
    /// </summary>
    public string Path { get; set; }

    /// <summary>
    /// Gets or sets the HTTP status code of the page.
    /// </summary>
    public int StatusCode { get; set; }

    /// <summary>
    /// Gets or sets where the page redirected to, when it did.
    /// </summary>
    public string RedirectedTo { get; set; }

    /// <summary>
    /// Gets or sets the page title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the error the page failed with, when it failed.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets whether the page was rendered with the designer's draft.
    /// </summary>
    public bool RenderedWithDraft { get; set; }

    /// <summary>
    /// Gets or sets the addresses of the stylesheets the page loads, in order.
    /// </summary>
    public IReadOnlyList<string> Stylesheets { get; set; } = [];

    /// <summary>
    /// Gets or sets the stylesheets the page loads, in order, with their size and whether they are the site's own
    /// resources, so a designer sees how much of the page's styling still comes from the theme.
    /// </summary>
    public IReadOnlyList<ThemeDraftStylesheet> StylesheetSizes { get; set; } = [];

    /// <summary>
    /// Gets or sets the addresses of the scripts the page loads, in order.
    /// </summary>
    public IReadOnlyList<string> Scripts { get; set; } = [];

    /// <summary>
    /// Gets or sets the templates that rendered the page, inner parts first: the name a template must take to replace
    /// each part, and the file or template used now.
    /// </summary>
    public IReadOnlyList<ThemeDraftRenderedTemplate> RenderedTemplates { get; set; } = [];

    /// <summary>
    /// Gets or sets the markup of the page's body, without scripts, styles and extra white space, cut to a length a
    /// model can read.
    /// </summary>
    public string BodyMarkup { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="BodyMarkup"/> was cut.
    /// </summary>
    public bool Truncated { get; set; }
}
