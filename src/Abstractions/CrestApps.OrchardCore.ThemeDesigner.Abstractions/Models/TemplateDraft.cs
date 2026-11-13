namespace CrestApps.OrchardCore.ThemeDesigner.Models;

/// <summary>
/// A shape template changed in a theme draft.
/// </summary>
public sealed class TemplateDraft
{
    /// <summary>
    /// Gets or sets whether the template is a front-end or an admin template.
    /// </summary>
    public ThemeDesignScope Scope { get; set; }

    /// <summary>
    /// Gets or sets the shape the template renders, such as <c>Layout</c>, <c>Content__BlogPost</c> or
    /// <c>Content_Summary__Article</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the Liquid template.
    /// </summary>
    public string Content { get; set; }

    /// <summary>
    /// Gets or sets what the template is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether publishing the draft removes the published template, so the theme's own template is used
    /// again.
    /// </summary>
    public bool Removed { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the change was last made.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }
}
