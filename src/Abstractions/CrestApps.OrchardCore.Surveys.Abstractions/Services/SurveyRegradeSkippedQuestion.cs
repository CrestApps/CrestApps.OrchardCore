namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// A question of one version that a regrade left alone because the other version's key does not fit its choices.
/// </summary>
public sealed class SurveyRegradeSkippedQuestion
{
    /// <summary>
    /// Gets or sets the number of the version whose responses kept the question's scores.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the question id.
    /// </summary>
    public string QuestionId { get; set; }

    /// <summary>
    /// Gets or sets the question's title, or its name when it has no title.
    /// </summary>
    public string Title { get; set; }
}
