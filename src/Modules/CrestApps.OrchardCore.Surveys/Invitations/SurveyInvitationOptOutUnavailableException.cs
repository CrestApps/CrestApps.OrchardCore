namespace CrestApps.OrchardCore.Surveys.Invitations;

/// <summary>
/// Thrown by the survey invitations consent source when the opt-out list's key can't be read, so it can't tell whether
/// an address unsubscribed. A send that asks the consent service fails (and is retried later) instead of either
/// mailing someone who may have unsubscribed or recording everyone as opted out.
/// </summary>
internal sealed class SurveyInvitationOptOutUnavailableException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutUnavailableException"/> class.
    /// </summary>
    public SurveyInvitationOptOutUnavailableException()
        : base("The survey invitation opt-out list can't be read because its Data Protection key is missing.")
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    public SurveyInvitationOptOutUnavailableException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationOptOutUnavailableException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The inner exception.</param>
    public SurveyInvitationOptOutUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
