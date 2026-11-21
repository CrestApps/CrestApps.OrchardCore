namespace CrestApps.OrchardCore.Surveys.Core.Controls.Number;

/// <summary>
/// The settings of a number question.
/// </summary>
public sealed class NumberSettings
{
    /// <summary>
    /// Gets or sets the smallest value, or <see langword="null"/>.
    /// </summary>
    public decimal? Min { get; set; }

    /// <summary>
    /// Gets or sets the largest value, or <see langword="null"/>.
    /// </summary>
    public decimal? Max { get; set; }

    /// <summary>
    /// Gets or sets the step, or <see langword="null"/> for any value.
    /// </summary>
    public decimal? Step { get; set; }

    /// <summary>
    /// Gets or sets the most decimals, or <see langword="null"/> for any.
    /// </summary>
    public int? Decimals { get; set; }

    /// <summary>
    /// Gets or sets the unit shown after the box, such as km.
    /// </summary>
    public string Unit { get; set; }

    /// <summary>
    /// Gets or sets how the question shows.
    /// </summary>
    public NumberDisplay Display { get; set; }
}
