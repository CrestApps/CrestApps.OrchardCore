namespace CrestApps.OrchardCore.Surveys.Core.Controls.Rating;

/// <summary>
/// The settings of a rating question.
/// </summary>
public sealed class RatingSettings
{
    /// <summary>
    /// Gets or sets the lowest value.
    /// </summary>
    public int Min { get; set; } = 1;

    /// <summary>
    /// Gets or sets the highest value.
    /// </summary>
    public int Max { get; set; } = 5;

    /// <summary>
    /// Gets or sets how the scale shows.
    /// </summary>
    public RatingStyle Style { get; set; }

    /// <summary>
    /// Gets or sets the label of the lowest value.
    /// </summary>
    public string MinLabel { get; set; }

    /// <summary>
    /// Gets or sets the label of the highest value.
    /// </summary>
    public string MaxLabel { get; set; }

    /// <summary>
    /// Gets or sets the score results report.
    /// </summary>
    public RatingMetric Metric { get; set; }
}
