namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// The outcome of delivering an invitation or a reminder (see <see cref="ISurveyInvitationSender"/>).
/// </summary>
public enum SurveyInvitationDelivery
{
    /// <summary>
    /// It was delivered.
    /// </summary>
    Delivered,

    /// <summary>
    /// It can't be delivered now, but may later: the mail server failed or is down, or no e-mail service is turned on.
    /// A first send is held and tried again by the hourly reminder task; a reminder stays due.
    /// </summary>
    RetryLater,

    /// <summary>
    /// It can never be delivered: the user no longer exists, or there is no address. A first send is marked bounced.
    /// </summary>
    Undeliverable,
}
