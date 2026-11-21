namespace CrestApps.OrchardCore.Surveys.Core.Controls.Date;

/// <summary>
/// The settings of a date question. Answers keep the wall-clock date and time the respondent entered.
/// </summary>
public sealed class DateSettings
{
    /// <summary>
    /// Gets or sets what the question asks for.
    /// </summary>
    public DateMode Mode { get; set; }

    /// <summary>
    /// Gets or sets the earliest value, or <see langword="null"/>.
    /// </summary>
    public DateTime? Min { get; set; }

    /// <summary>
    /// Gets or sets the latest value, or <see langword="null"/>.
    /// </summary>
    public DateTime? Max { get; set; }
}
