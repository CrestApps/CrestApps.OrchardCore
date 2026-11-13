namespace CrestApps.OrchardCore.ThemeDesigner.Services;

/// <summary>
/// The template a theme or module renders a shape with.
/// </summary>
public sealed class ThemeShapeSource
{
    /// <summary>
    /// Gets or sets the shape type or alternate.
    /// </summary>
    public string ShapeType { get; set; }

    /// <summary>
    /// Gets or sets the file or code that renders the shape.
    /// </summary>
    public string BindingSource { get; set; }

    /// <summary>
    /// Gets or sets the template language: <c>liquid</c>, <c>razor</c>, or <c>code</c> for a shape rendered by C#.
    /// </summary>
    public string Language { get; set; }

    /// <summary>
    /// Gets or sets the template's source, or <see langword="null"/> when it can't be read.
    /// </summary>
    public string Content { get; set; }
}
