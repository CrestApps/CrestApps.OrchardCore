using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Surveys.Core.Indexes;

/// <summary>
/// Finds invitations by survey, status, recipient and token.
/// </summary>
public sealed class SurveyInvitationIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the collector.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the id of the user, or null.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the recipient as Kind:Id, or null.
    /// </summary>
    public string RecipientRef { get; set; }

    /// <summary>
    /// Gets or sets the hash of the token.
    /// </summary>
    public string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets when it was sent, in UTC.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets when it expires, in UTC.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }
}
