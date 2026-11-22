using YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// One row per answer value of a completed, non-test response (a chosen choice, a number, a date or a text). Results
/// aggregate these rows in SQL.
/// </summary>
public sealed class SurveyAnswerIndex : MapIndex
{
    /// <summary>
    /// The longest text kept in <see cref="TextValue"/>.
    /// </summary>
    public const int TextLength = 255;

    /// <summary>
    /// The longest id kept in <see cref="QuestionId"/>, <see cref="RowId"/> and <see cref="ChoiceId"/>.
    /// </summary>
    public const int KeyLength = 64;

    /// <summary>
    /// The size a <see cref="NumberValue"/> must stay below: the column keeps 14 digits before the decimal point (the
    /// default decimal precision of 19 with a scale of 5).
    /// </summary>
    public const decimal NumberLimit = 100_000_000_000_000m;

    /// <summary>
    /// Gets or sets the id of the response.
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the number of the version the response ran on.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the stable id of the question.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the variable code of the question in that version, cut to 100 characters.
    /// </summary>
    public string QuestionName { get; set; }

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
    /// Gets or sets the text value, cut to 255 characters, or <see langword="null"/>.
    /// </summary>
    public string TextValue { get; set; }

    /// <summary>
    /// Gets or sets the score of the question; set on the first row of each question only, so sums count it once.
    /// </summary>
    public decimal? Score { get; set; }

    /// <summary>
    /// Gets or sets the most points of the question; set on the first row of each question only.
    /// </summary>
    public decimal? MaxPoints { get; set; }

    /// <summary>
    /// Gets or sets when the response was completed, in UTC.
    /// </summary>
    public DateTime CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the id of the collector the response came through.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the kind of the response's context, or <see langword="null"/>.
    /// </summary>
    public string ContextKind { get; set; }

    /// <summary>
    /// Gets or sets the response's context as Kind:Id, or <see langword="null"/>.
    /// </summary>
    public string ContextRef { get; set; }
}
