using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A shape template with its published and drafted content.
/// </summary>
public sealed class ShapeTemplateDetails
{
    /// <summary>
    /// Gets or sets whether the template is a front-end or an admin template.
    /// </summary>
    public ThemeDesignScope Scope { get; set; }

    /// <summary>
    /// Gets or sets the shape the template renders.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the published template's description, if any.
    /// </summary>
    public string PublishedDescription { get; set; }

    /// <summary>
    /// Gets or sets the published Liquid, or <see langword="null"/> when no template is published.
    /// </summary>
    public string PublishedContent { get; set; }

    /// <summary>
    /// Gets or sets the draft's description, if any.
    /// </summary>
    public string DraftDescription { get; set; }

    /// <summary>
    /// Gets or sets the draft's Liquid, or <see langword="null"/> when the draft leaves the template alone or removes it.
    /// </summary>
    public string DraftContent { get; set; }

    /// <summary>
    /// Gets or sets what the draft does to the template, or <see langword="null"/> when the draft leaves it alone.
    /// </summary>
    public ThemeDesignChangeStatus? DraftStatus { get; set; }
}
