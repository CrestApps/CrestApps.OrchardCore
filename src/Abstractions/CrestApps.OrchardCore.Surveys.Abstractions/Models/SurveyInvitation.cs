using CrestApps.Core.Models;
using CrestApps.OrchardCore.Parties.Models;

namespace CrestApps.OrchardCore.Surveys.Models;

/// <summary>
/// A personal invitation to respond.
/// </summary>
public sealed class SurveyInvitation : CatalogItem
{
    /// <summary>
    /// Gets or sets the id of the survey.
    /// </summary>
    public string SurveyId { get; set; }

    /// <summary>
    /// Gets or sets the id of the invitation collector.
    /// </summary>
    public string CollectorId { get; set; }

    /// <summary>
    /// Gets or sets the party invited, or null.
    /// </summary>
    public PartyRef? Recipient { get; set; }

    /// <summary>
    /// Gets or sets the id of the user invited, or null.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// Gets or sets the e-mail address invited, or null.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the phone number invited, or null.
    /// </summary>
    public string Phone { get; set; }

    /// <summary>
    /// Gets or sets the name of the person invited.
    /// </summary>
    public string DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the SHA-256 of the personal link token.
    /// </summary>
    public string TokenHash { get; set; }

    /// <summary>
    /// Gets or sets the value an anonymous response's invitation seal is looked up by: an HMAC of the seal keyed by the
    /// survey's secret, so only the secret links a response to its invitation. Null for invitations made before it
    /// was kept.
    /// </summary>
    public string SealLookup { get; set; }

    /// <summary>
    /// Gets or sets where the invitation stands.
    /// </summary>
    public SurveyInvitationStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was sent, in UTC.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets when the last reminder was sent, in UTC.
    /// </summary>
    public DateTime? LastReminderUtc { get; set; }

    /// <summary>
    /// Gets or sets how many reminders were sent.
    /// </summary>
    public int RemindersSent { get; set; }

    /// <summary>
    /// Gets or sets the id of the response, or null (never set for anonymous surveys).
    /// </summary>
    public string ResponseId { get; set; }

    /// <summary>
    /// Gets or sets when the link expires, in UTC.
    /// </summary>
    public DateTime? ExpiresUtc { get; set; }
}
