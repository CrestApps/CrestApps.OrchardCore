using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds responses for lists, limits, grading, deadlines and data sets.
/// </summary>
public sealed class SurveyResponseIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the version.
    /// </summary>
    public string SurveyVersionId { get; set; }

    /// <summary>
    /// Gets or sets the version number.
    /// </summary>
    public int VersionNumber { get; set; }

    /// <summary>
    /// Gets or sets the readable number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the grading status.
    /// </summary>
    public string Grading { get; set; }

    /// <summary>
    /// Gets or sets the id of the user, or null.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the respondent as Kind:Id, or null.
    /// </summary>
    public string RespondentRef { get; set; }

    /// <summary>
    /// Gets or sets the context as Kind:Id, or null.
    /// </summary>
    public string ContextRef { get; set; }

    /// <summary>
    /// Gets or sets the kind of the context, or null.
    /// </summary>
    public string ContextKind { get; set; }

    /// <summary>
    /// Gets or sets the id of the collector.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the id of the invitation, or null.
    /// </summary>
    public string InvitationId { get; set; }

    /// <summary>
    /// Gets or sets the attempt number.
    /// </summary>
    public int AttemptNumber { get; set; }

    /// <summary>
    /// Gets or sets whether it is a test response.
    /// </summary>
    public bool IsTest { get; set; }

    /// <summary>
    /// Gets or sets when it started, in UTC.
    /// </summary>
    public DateTime StartedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last saved, in UTC.
    /// </summary>
    public DateTime LastActivityUtc { get; set; }

    /// <summary>
    /// Gets or sets when it ended, in UTC.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets when time runs out, in UTC.
    /// </summary>
    public DateTime? DeadlineUtc { get; set; }

    /// <summary>
    /// Gets or sets the score as a percent.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets whether it passed.
    /// </summary>
    public bool? Passed { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC, for incremental reads.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether retention anonymized the response.
    /// </summary>
    public bool Anonymized { get; set; }

    /// <summary>
    /// Gets or sets how many times in a row background upkeep failed on the response.
    /// </summary>
    public int UpkeepFailures { get; set; }
}
