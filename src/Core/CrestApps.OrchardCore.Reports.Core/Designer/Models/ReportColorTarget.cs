namespace CrestApps.OrchardCore.Reports.Designer;

/// <summary>
/// Identifies what part of a cell a color paints.
/// </summary>
public enum ReportColorTarget
{
    /// <summary>
    /// The color fills the cell. The text turns white on a dark color, so it stays readable.
    /// </summary>
    Background,

    /// <summary>
    /// The color is the color of the text.
    /// </summary>
    Text,
}
