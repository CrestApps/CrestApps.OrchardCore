namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// Which bank questions to read.
/// </summary>
public sealed class BankQuestionQuery
{
    /// <summary>
    /// Gets or sets the bank.
    /// </summary>
    public string BankId { get; set; }

    /// <summary>
    /// Gets or sets the question type, or <see langword="null"/> for all.
    /// </summary>
    public string ControlType { get; set; }

    /// <summary>
    /// Gets or sets a tag the questions must have, or <see langword="null"/>.
    /// </summary>
    public string Tag { get; set; }

    /// <summary>
    /// Gets or sets the difficulty, or <see langword="null"/> for all.
    /// </summary>
    public int? Difficulty { get; set; }

    /// <summary>
    /// Gets or sets whether retired questions are read: <see langword="null"/> for all, <see langword="true"/> for retired
    /// questions only, <see langword="false"/> for active questions only.
    /// </summary>
    public bool? Retired { get; set; } = false;

    /// <summary>
    /// Gets or sets a search on the question text, or <see langword="null"/>.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets how many to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many to read, at most <see cref="SurveysConstants.PageSize"/>.
    /// </summary>
    public int Take { get; set; } = 50;
}
