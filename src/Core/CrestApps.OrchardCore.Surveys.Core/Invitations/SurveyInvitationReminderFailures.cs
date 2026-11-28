using CrestApps.Core;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Counts the failed attempts at an invitation's next reminder, like <see cref="SurveyInvitationHold"/> does for a first
/// send: a reminder the mail server refused is tried again on the hourly reminder runs, and after
/// <see cref="SurveyInvitationHold.MaxSendAttempts"/> failures (or at once when it can never be delivered) the invitation
/// gets no more reminders. The invitation itself is left open, so its link, delivered with the first send, still works.
/// Kept in the invitation's properties; removed when a reminder is delivered or the recipient is invited again.
/// </summary>
public sealed class SurveyInvitationReminderFailures
{
    /// <summary>
    /// Gets or sets how many times sending the next reminder failed at the mail server, counted as
    /// <see cref="SurveyInvitationHold.FailedAttempts"/> is: an outage, which fails every e-mail, uses up no attempts.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Gets or sets when a failure last used up an attempt, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? LastCountedUtc { get; set; }

    /// <summary>
    /// Gets or sets when sending the next reminder last failed at the mail server, in UTC, or <see langword="null"/>.
    /// </summary>
    public DateTime? LastFailedUtc { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether reminders stopped for the invitation.
    /// </summary>
    public bool Stopped { get; set; }

    /// <summary>
    /// Gets whether reminders stopped for an invitation.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    /// <returns><see langword="true"/> when they stopped.</returns>
    public static bool HaveStopped(SurveyInvitation invitation)
        => invitation is not null && invitation.TryGet<SurveyInvitationReminderFailures>(out var failures) && failures?.Stopped == true;
}
