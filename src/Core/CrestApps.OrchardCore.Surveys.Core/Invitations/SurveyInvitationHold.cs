namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Marks a pending invitation whose first send was held back: the site's daily e-mail limit was reached, the opt-out
/// list couldn't be read, no e-mail service is turned on, or the mail server failed. The hourly reminder task sends held
/// invitations on a later run; invitations that were only created, never sent, are left alone. Kept in the invitation's
/// properties and removed once it is sent.
/// </summary>
public sealed class SurveyInvitationHold
{
    /// <summary>
    /// How many times a first send may fail at the mail server before the invitation is marked bounced.
    /// </summary>
    public const int MaxSendAttempts = 5;

    /// <summary>
    /// Gets or sets when the invitation was first held back, in UTC.
    /// </summary>
    public DateTime HeldUtc { get; set; }

    /// <summary>
    /// Gets or sets how many times sending it failed at the mail server; at <see cref="MaxSendAttempts"/> it is marked
    /// bounced. A failure counts only when the mail server took another e-mail of the same send, so an outage, which
    /// fails every e-mail, uses up no attempts.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets when sending it last failed at the mail server, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? LastFailedUtc { get; set; }
}
