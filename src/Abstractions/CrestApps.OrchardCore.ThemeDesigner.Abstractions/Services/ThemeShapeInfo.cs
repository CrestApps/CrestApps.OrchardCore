namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// A shape the active theme can render, and where its template comes from.
/// </summary>
public sealed class ThemeShapeInfo
{
    /// <summary>
    /// Gets or sets the shape type or alternate, which is also the name a template for it takes, such as
    /// <c>Content__BlogPost</c>.
    /// </summary>
    public string ShapeType { get; set; }

    /// <summary>
    /// Gets or sets the file or code that renders the shape today, such as <c>Areas/TheTheme/Views/Layout.cshtml</c>.
    /// </summary>
    public string BindingSource { get; set; }

    /// <summary>
    /// Gets or sets whether a published template replaces it.
    /// </summary>
    public bool HasPublishedTemplate { get; set; }

    /// <summary>
    /// Gets or sets whether the draft changes its template.
    /// </summary>
    public bool HasDraftTemplate { get; set; }
}
