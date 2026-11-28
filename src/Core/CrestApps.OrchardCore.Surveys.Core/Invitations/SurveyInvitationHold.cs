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
    /// How long an e-mail that fails alone (nothing else of its send was tried after it, and nothing went out before it)
    /// waits before such a failure uses up another attempt: an outage can't be told apart from an address the mail
    /// server refuses, so a lone failure counts at most about once a day. An address that is always refused still
    /// bounces, after a few days, and a mail outage of a few hours costs an invitation at most one attempt.
    /// </summary>
    public static readonly TimeSpan LoneFailureInterval = TimeSpan.FromHours(23);

    /// <summary>
    /// Gets or sets how many times sending it failed at the mail server; at <see cref="MaxSendAttempts"/> it is marked
    /// bounced. A failure is judged by the next e-mail of the same send: when it goes out, the failure was this
    /// address's and counts; when it fails too, the mail server is taken to be down and neither counts. A failure with
    /// no e-mail after it counts when an earlier e-mail of the send went out, and otherwise at most once per
    /// <see cref="LoneFailureInterval"/>.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets when a failure last used up an attempt, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? LastCountedUtc { get; set; }

    /// <summary>
    /// Gets or sets when sending it last failed at the mail server, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? LastFailedUtc { get; set; }
}
