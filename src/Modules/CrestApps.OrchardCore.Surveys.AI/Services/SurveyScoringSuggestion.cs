namespace CrestApps.OrchardCore.Surveys.AI.Services;

/// <summary>
/// A suggested score and comment for one answer. The grader decides.
/// </summary>
public sealed class SurveyScoringSuggestion
{
    /// <summary>
    /// Gets or sets the suggested points, or <see langword="null"/>.
    /// </summary>
    public decimal? Points { get; set; }

    /// <summary>
    /// Gets or sets the suggested comment to the respondent, or <see langword="null"/>.
    /// </summary>
    public string Comment { get; set; }
}
