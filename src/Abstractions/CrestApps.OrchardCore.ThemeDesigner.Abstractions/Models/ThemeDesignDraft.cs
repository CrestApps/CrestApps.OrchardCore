namespace CrestApps.OrchardCore.ThemeDesigner.Models;

/// <summary>
/// One designer's unpublished changes to the front-end templates, the admin templates and the site resources. Only
/// the designer sees them, while previewing, until they publish the draft.
/// </summary>
public sealed class ThemeDesignDraft
{
    /// <summary>
    /// Gets or sets the identifier of the user the draft belongs to.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the name of the user the draft belongs to.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets whether the designer sees the draft while browsing the site.
    /// </summary>
    public bool Preview { get; set; } = true;

    /// <summary>
    /// Gets or sets the changed templates.
    /// </summary>
    public List<TemplateDraft> Templates { get; set; } = [];

    /// <summary>
    /// Gets or sets the changed site resources.
    /// </summary>
    public List<SiteResourceDraft> Resources { get; set; } = [];

    /// <summary>
    /// Gets or sets the UTC time the draft was started.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the draft last changed.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets the number of changes in the draft.
    /// </summary>
    public int ChangeCount
        => (Templates?.Count ?? 0) + (Resources?.Count ?? 0);

    /// <summary>
    /// Finds the draft of a template.
    /// </summary>
    /// <param name="scope">The side of the site the template belongs to.</param>
    /// <param name="name">The template name.</param>
    public TemplateDraft FindTemplate(ThemeDesignScope scope, string name)
        => Templates?.FirstOrDefault(template => template.Scope == scope && string.Equals(template.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Finds the draft of a site resource.
    /// </summary>
    /// <param name="name">The resource name.</param>
    public SiteResourceDraft FindResource(string name)
        => Resources?.FirstOrDefault(resource => string.Equals(resource.Name, name, StringComparison.OrdinalIgnoreCase));
}
