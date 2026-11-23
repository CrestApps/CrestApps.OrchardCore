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
    /// Gets or sets the value an anonymous response's invitation seal is looked up by, or null.
    /// </summary>
    public string SealLookup { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address invited, in lowercase, or null; personal data export and erasure find guest
    /// invitations by it.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets when the last reminder was sent, in UTC, or null; the daily e-mail limit counts it.
    /// </summary>
    public DateTime? LastReminderUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was sent, in UTC.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets when it expires, in UTC.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether it is a first send held back for a later run (pending, with a
    /// hold mark), so the reminder task finds held invitations without reading the never-sent ones.
    /// </summary>
    public bool Held { get; set; }
}
