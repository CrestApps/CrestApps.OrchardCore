namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// Renders a page of the site as the signed-in designer, with their theme draft, so a designer or the AI can check
/// what a change looks like before publishing it.
/// </summary>
public interface IThemeDraftPagePreviewer
{
    /// <summary>
    /// Renders the page at <paramref name="path"/>, a path on this site, as the signed-in user.
    /// </summary>
    /// <param name="path">The path of the page, such as <c>/</c>.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page, or <see langword="null"/> when the path isn't a page of this site or no user is signed in.</returns>
    Task<ThemeDraftPagePreview> RenderAsync(string path, CancellationToken cancellationToken = default);
}
