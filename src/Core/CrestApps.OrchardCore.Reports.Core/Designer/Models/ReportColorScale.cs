namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// A color scale that shades the numbers of a column from one color at its smallest value to another at its largest,
/// like a heat map, so the high and low values of a table stand out.
/// </summary>
public sealed class ReportColorScale
{
    /// <summary>
    /// Gets or sets the color of the smallest value, as a <c>#RRGGBB</c> hex value.
    /// </summary>
    public string LowColor { get; set; }

    /// <summary>
    /// Gets or sets the color of the value halfway between the smallest and the largest, or <see langword="null"/> to
    /// blend the low color straight into the high one.
    /// </summary>
    public string MidColor { get; set; }

    /// <summary>
    /// Gets or sets the color of the largest value, as a <c>#RRGGBB</c> hex value.
    /// </summary>
    public string HighColor { get; set; }

    /// <summary>
    /// Gets or sets what part of the cell the scale paints.
    /// </summary>
    public ReportColorTarget Target { get; set; }
}
