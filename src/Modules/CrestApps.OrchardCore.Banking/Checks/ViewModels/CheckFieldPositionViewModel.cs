namespace CrestApps.OrchardCore.Banking.Checks.ViewModels;

/// <summary>
/// The place of one field in the check layout editor. Numbers are bound as invariant text.
/// </summary>
public class CheckFieldPositionViewModel
{
    /// <summary>
    /// Gets or sets the field's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the field's label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the distance from the left edge, in points.
    /// </summary>
    public string X { get; set; }

    /// <summary>
    /// Gets or sets the distance from the top edge, in points.
    /// </summary>
    public string Y { get; set; }

    /// <summary>
    /// Gets or sets the width, in points.
    /// </summary>
    public string Width { get; set; }

    /// <summary>
    /// Gets or sets the text size, in points.
    /// </summary>
    public string FontSize { get; set; }

    /// <summary>
    /// Gets or sets whether the field is left off.
    /// </summary>
    public bool Hidden { get; set; }
}
