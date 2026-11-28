namespace CrestApps.OrchardCore.Surveys.Core.Controls.Slider;

/// <summary>
/// The settings of a slider question.
/// </summary>
public sealed class SliderSettings
{
    /// <summary>
    /// Gets or sets the lowest value.
    /// </summary>
    public decimal Min { get; set; }

    /// <summary>
    /// Gets or sets the highest value.
    /// </summary>
    public decimal Max { get; set; } = 100;

    /// <summary>
    /// Gets or sets the step.
    /// </summary>
    public decimal Step { get; set; } = 1;

    /// <summary>
    /// Gets or sets the label of the lowest value.
    /// </summary>
    public string MinLabel { get; set; }

    /// <summary>
    /// Gets or sets the label of the highest value.
    /// </summary>
    public string MaxLabel { get; set; }
}
