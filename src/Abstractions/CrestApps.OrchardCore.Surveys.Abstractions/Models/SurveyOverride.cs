using CrestApps.Core.Models;
using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// An accommodation for a respondent or an audience: extra time, extra attempts or another window.
/// </summary>
public sealed class SurveyOverride : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the user it applies to, or null.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the id of the user audience it applies to, or null.
    /// </summary>
    public string AudienceId { get; set; }

    /// <summary>
    /// Gets or sets the context it applies to, or null for any.
    /// </summary>
    public RecordRef? Context { get; set; }

    /// <summary>
    /// Gets or sets the extra minutes of the time limit.
    /// </summary>
    public int ExtraMinutes { get; set; }

    /// <summary>
    /// Gets or sets the extra attempts.
    /// </summary>
    public int ExtraAttempts { get; set; }

    /// <summary>
    /// Gets or sets another opening time, in UTC.
    /// </summary>
    public DateTime? OpensUtc { get; set; }

    /// <summary>
    /// Gets or sets another closing time, in UTC.
    /// </summary>
    public DateTime? ClosesUtc { get; set; }

    /// <summary>
    /// Gets or sets why it was granted.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the id of the user who granted it.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets when it was granted, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
