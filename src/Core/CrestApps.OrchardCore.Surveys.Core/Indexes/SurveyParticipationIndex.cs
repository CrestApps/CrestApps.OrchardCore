using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds participation counts for respondent limits.
/// </summary>
public sealed class SurveyParticipationIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the respondent key.
    /// </summary>
    public string RespondentKey { get; set; }

    /// <summary>
    /// Gets or sets the context key.
    /// </summary>
    public string ContextKey { get; set; }

    /// <summary>
    /// Gets or sets the day.
    /// </summary>
    public DateTime Day { get; set; }

    /// <summary>
    /// Gets or sets how many responses started.
    /// </summary>
    public int StartedCount { get; set; }

    /// <summary>
    /// Gets or sets how many responses were completed.
    /// </summary>
    public int CompletedCount { get; set; }
}
