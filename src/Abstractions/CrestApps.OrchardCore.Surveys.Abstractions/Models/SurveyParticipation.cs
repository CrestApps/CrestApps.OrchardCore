using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// How many responses one respondent started and completed on one day. It holds no response id, so limits never link a respondent to the answers of an anonymous survey.
/// </summary>
public sealed class SurveyParticipation : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the respondent key: a user id, or a pseudonymous key for anonymous surveys.
    /// </summary>
    public string RespondentKey { get; set; }

    /// <summary>
    /// Gets or sets the context, such as LearningEnrollment:123, or empty.
    /// </summary>
    public string ContextKey { get; set; }

    /// <summary>
    /// Gets or sets the day in the site time zone.
    /// </summary>
    public DateOnly Day { get; set; }

    /// <summary>
    /// Gets or sets how many responses started.
    /// </summary>
    public int StartedCount { get; set; }

    /// <summary>
    /// Gets or sets how many responses were completed.
    /// </summary>
    public int CompletedCount { get; set; }

    /// <summary>
    /// Gets or sets when the last response started, in UTC.
    /// </summary>
    public DateTime? LastStartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last response was completed, in UTC.
    /// </summary>
    public DateTime? LastCompletedUtc { get; set; }
}
