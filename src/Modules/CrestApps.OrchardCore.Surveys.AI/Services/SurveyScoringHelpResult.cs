namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// The result of asking for scoring help.
/// </summary>
public sealed class SurveyScoringHelpResult
{
    /// <summary>
    /// The response or question does not exist, needs no grader, or the user may not grade it.
    /// </summary>
    public const string NotFound = "NotFound";

    /// <summary>
    /// No AI model is set up.
    /// </summary>
    public const string NoModel = "NoModel";

    /// <summary>
    /// The answer has no text.
    /// </summary>
    public const string NoText = "NoText";

    /// <summary>
    /// The model gave no usable suggestion.
    /// </summary>
    public const string NoSuggestion = "NoSuggestion";

    /// <summary>
    /// Gets or sets the error, or <see langword="null"/>.
    /// </summary>
    public string Error { get; set; }

    /// <summary>
    /// Gets or sets the suggestion, or <see langword="null"/>.
    /// </summary>
    public SurveyScoringSuggestion Suggestion { get; set; }
}
