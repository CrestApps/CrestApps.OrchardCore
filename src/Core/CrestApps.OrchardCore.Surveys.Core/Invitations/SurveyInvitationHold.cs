namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Marks a pending invitation whose first send was held back: the site's daily e-mail limit was reached, or the
/// opt-out list couldn't be read. The hourly reminder task sends held invitations on a later run; invitations that
/// were only created, never sent, are left alone. Kept in the invitation's properties and removed once it is sent.
/// </summary>
public sealed class SurveyInvitationHold
{
    /// <summary>
    /// Gets or sets when the invitation was first held back, in UTC.
    /// </summary>
    public DateTime HeldUtc { get; set; }
}
