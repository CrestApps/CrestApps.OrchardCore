namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// What the opt-out list says about an e-mail address.
/// </summary>
internal enum SurveyInvitationOptOutState
{
    /// <summary>
    /// The address did not unsubscribe.
    /// </summary>
    NotOptedOut,

    /// <summary>
    /// The address unsubscribed.
    /// </summary>
    OptedOut,

    /// <summary>
    /// The list's key can't be read (missing Data Protection keys), so nobody can tell; nothing may be sent to the
    /// address and nothing about it may be recorded as an opt-out until the keys are back.
    /// </summary>
    Unknown,
}
