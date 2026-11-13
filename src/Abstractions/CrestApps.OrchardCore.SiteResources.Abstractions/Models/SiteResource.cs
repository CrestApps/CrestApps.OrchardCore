using System.Text.Json;
using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Models;

namespace CrestApps.OrchardCore.SiteResources.Models;

/// <summary>
/// A stylesheet or a script kept in the site. It is minified when it is saved, served from its own address, and added
/// to every page of its target or only to the pages whose templates ask for it by <see cref="Name"/>.
/// </summary>
public sealed class SiteResource : CatalogItem, INameAwareModel, IAuditedModel, ICloneable<SiteResource>
{
    /// <summary>
    /// Gets or sets the unique name of the resource. Templates ask for an on-demand resource by this name, and its
    /// address ends with it. Letters, digits, dots, dashes and underscores only.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the resource is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether the resource is a stylesheet or a script.
    /// </summary>
    public SiteResourceType Type { get; set; }

    /// <summary>
    /// Gets or sets the CSS or JavaScript as it was written.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets whether the resource is served minified.
    /// </summary>
    public bool Minify { get; set; } = true;

    /// <summary>
    /// Gets or sets what is served: the minified content, or <see cref="Content"/> when the resource is not
    /// minified or could not be minified. Set when the resource is saved.
    /// </summary>
    public string ServedContent { get; set; }

    /// <summary>
    /// Gets or sets a short hash of <see cref="ServedContent"/>, added to the resource's address so browsers fetch it
    /// again only after it changes. Set when the resource is saved.
    /// </summary>
    public string Version { get; set; }

    /// <summary>
    /// Gets or sets the pages the resource is offered to.
    /// </summary>
    public SiteResourceTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the themes the resource is limited to, such as <c>TheBlogTheme</c>: a page gets
    /// the resource only while one of them renders it. Empty for every theme.
    /// </summary>
    public List<string> Themes { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the resource is added to every page of its target or only on demand.
    /// </summary>
    public SiteResourcePlacement Placement { get; set; }

    /// <summary>
    /// Gets or sets where a script is added to the page. Stylesheets always go in the head.
    /// </summary>
    public SiteResourceLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the names of the resources that must be on the page first, such as <c>jQuery</c>,
    /// <c>bootstrap</c> or another site resource.
    /// </summary>
    public List<string> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets the order in which the resources added to every page are added, smallest first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets whether the resource is offered to pages.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <inheritdoc />
    public DateTime CreatedUtc { get; set; }

    /// <inheritdoc />
    public DateTime? ModifiedUtc { get; set; }

    /// <inheritdoc />
    public string Author { get; set; }

    /// <inheritdoc />
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets the resource manager type of the resource: <c>stylesheet</c> or <c>script</c>.
    /// </summary>
    public string GetResourceType()
        => Type == SiteResourceType.Script ? SiteResourcesConstants.ScriptResourceType : SiteResourcesConstants.StylesheetResourceType;

    /// <summary>
    /// Gets the file extension the resource is served with: <c>css</c> or <c>js</c>.
    /// </summary>
    public string GetExtension()
        => Type == SiteResourceType.Script ? "js" : "css";

    /// <summary>
    /// Gets whether the resource is enabled and offered to a page: an admin page when <paramref name="isAdmin"/> is
    /// <see langword="true"/> or a front-end page otherwise, rendered by the theme <paramref name="themeId"/>.
    /// </summary>
    /// <param name="isAdmin">Whether the page is an admin page.</param>
    /// <param name="themeId">The identifier of the theme that renders the page, or <see langword="null"/> when unknown.</param>
    public bool IsOfferedTo(bool isAdmin, string themeId)
        => Enabled &&
            (Target == SiteResourceTarget.Everywhere || (isAdmin ? Target == SiteResourceTarget.Admin : Target == SiteResourceTarget.Frontend)) &&
            IsForTheme(themeId);

    /// <summary>
    /// Gets whether the resource applies to pages the theme <paramref name="themeId"/> renders: it names no theme, or
    /// names this one.
    /// </summary>
    /// <param name="themeId">The theme identifier.</param>
    public bool IsForTheme(string themeId)
        => Themes is not { Count: > 0 } ||
            (!string.IsNullOrEmpty(themeId) && Themes.Contains(themeId, StringComparer.OrdinalIgnoreCase));

    /// <inheritdoc />
    public SiteResource Clone()
        => JsonSerializer.Deserialize<SiteResource>(JsonSerializer.Serialize(this));
}
