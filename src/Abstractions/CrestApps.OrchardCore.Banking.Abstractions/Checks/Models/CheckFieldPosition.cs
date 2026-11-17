namespace CrestApps.OrchardCore.Banking.Checks.Models;

/// <summary>
/// Where a field prints on the check, in points (a point is 1/72 inch) from the top left corner of the check.
/// </summary>
public sealed class CheckFieldPosition
{
    /// <summary>
    /// Gets or sets the distance from the left edge of the check.
    /// </summary>
    public double X { get; set; }

    /// <summary>
    /// Gets or sets the distance from the top edge of the check to the top of the field.
    /// </summary>
    public double Y { get; set; }

    /// <summary>
    /// Gets or sets the width the field may take; 0 for no limit.
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets the size of the text, in points.
    /// </summary>
    public double FontSize { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether the field is left off.
    /// </summary>
    public bool Hidden { get; set; }
}
