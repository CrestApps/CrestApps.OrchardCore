namespace CrestApps.OrchardCore.Surveys.Controls;

/// <summary>
/// One row of the answer index for a completed answer. Results aggregate these rows in SQL.
/// </summary>
public sealed class SurveyAnswerIndexRow
{
    /// <summary>
    /// Gets or sets the id of the row, or <see langword="null"/>.
    /// </summary>
    public string RowId { get; set; }

    /// <summary>
    /// Gets or sets the id of the chosen choice, or <see langword="null"/>.
    /// </summary>
    public string ChoiceId { get; set; }

    /// <summary>
    /// Gets or sets the number value, or <see langword="null"/>.
    /// </summary>
    public decimal? NumberValue { get; set; }

    /// <summary>
    /// Gets or sets the date value, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? DateValue { get; set; }

    /// <summary>
    /// Gets or sets the text value (cut to 255 characters), or <see langword="null"/>.
    /// </summary>
    public string TextValue { get; set; }
}
