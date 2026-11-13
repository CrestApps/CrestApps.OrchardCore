namespace CrestApps.OrchardCore.SiteResources.Services;

/// <summary>
/// The site resources offered to the page being rendered, which an <see cref="ISiteResourcesHandler"/> may change,
/// for instance to show someone a version they are working on.
/// </summary>
public sealed class SiteResourcesResolvingContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SiteResourcesResolvingContext"/> class.
    /// </summary>
    /// <param name="isAdmin">Whether the page is an admin page.</param>
    /// <param name="themeId">The identifier of the theme that renders the page.</param>
    /// <param name="entries">The published resources offered to the page.</param>
    public SiteResourcesResolvingContext(bool isAdmin, string themeId, IEnumerable<SiteResourceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        IsAdmin = isAdmin;
        ThemeId = themeId;
        Entries = [.. entries];
    }

    /// <summary>
    /// Gets whether the page is an admin page.
    /// </summary>
    public bool IsAdmin { get; }

    /// <summary>
    /// Gets the identifier of the theme that renders the page, or <see langword="null"/> when unknown.
    /// </summary>
    public string ThemeId { get; }

    /// <summary>
    /// Gets the resources offered to the page. A handler may add, replace or remove entries.
    /// </summary>
    public List<SiteResourceEntry> Entries { get; }
}
