namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// Statistics of the number values of a question.
/// </summary>
public sealed class SurveyNumberStatistics
{
    /// <summary>
    /// Gets or sets how many values there are.
    /// </summary>
    public long Count { get; set; }

    /// <summary>
    /// Gets or sets the average, or <see langword="null"/>.
    /// </summary>
    public decimal? Average { get; set; }

    /// <summary>
    /// Gets or sets the smallest value, or <see langword="null"/>.
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets the largest value, or <see langword="null"/>.
    /// </summary>
    public decimal? Max { get; set; }

    /// <summary>
    /// Gets or sets the median, or <see langword="null"/>.
    /// </summary>
    public decimal? Median { get; set; }

    /// <summary>
    /// Gets or sets how many responses gave each value, by value.
    /// </summary>
    public Dictionary<decimal, long> Distribution { get; set; } = [];
}
