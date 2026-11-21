namespace CrestApps.OrchardCore.Surveys.Services;

/// <summary>
/// The bank questions of a survey's published version that changed since it was published.
/// </summary>
public sealed class SurveyBankChange
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the number of the published version compared.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the ids of the questions edited since publishing.
    /// </summary>
    public List<string> ChangedQuestionIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the ids of the questions retired or deleted since publishing.
    /// </summary>
    public List<string> RemovedQuestionIds { get; set; } = [];
}
