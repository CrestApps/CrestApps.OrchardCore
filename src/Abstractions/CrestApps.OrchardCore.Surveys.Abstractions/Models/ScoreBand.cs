namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A result message for responses whose percent reaches a minimum.
/// </summary>
public sealed class ScoreBand
{
    /// <summary>
    /// Gets or sets the stable id.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the lowest percent of the band.
    /// </summary>
    public decimal MinPercent { get; set; }

    /// <summary>
    /// Gets or sets the label, such as Excellent.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the message shown to the respondent.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether the band passes or fails, or null to follow the pass mark.
    /// </summary>
    public bool? Passing { get; set; }
}
