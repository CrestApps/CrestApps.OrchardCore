using CrestApps.OrchardCore.ThemeDesigner.Models;

namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A shape template, published or drafted, without its content.
/// </summary>
public sealed class ShapeTemplateSummary
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
    /// Gets or sets what the template is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets whether a published template exists.
    /// </summary>
    public bool Published { get; set; }

    /// <summary>
    /// Gets or sets what the draft does to the template, or <see langword="null"/> when the draft leaves it alone.
    /// </summary>
    public ThemeDesignChangeStatus? DraftStatus { get; set; }

    /// <summary>
    /// Gets or sets the length of the template the designer sees: the draft's when there is one.
    /// </summary>
    public int Length { get; set; }
}
