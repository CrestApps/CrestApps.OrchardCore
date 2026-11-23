namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// The statuses Mailchimp gives an audience member.
/// </summary>
public static class MailchimpMemberStatuses
{
    /// <summary>
    /// Receives the audience's campaigns.
    /// </summary>
    public const string Subscribed = "subscribed";

    /// <summary>
    /// Left the audience, or was removed from it.
    /// </summary>
    public const string Unsubscribed = "unsubscribed";

    /// <summary>
    /// Removed by Mailchimp because mail to the address bounces.
    /// </summary>
    public const string Cleaned = "cleaned";

    /// <summary>
    /// Waits for the confirmation of Mailchimp's double opt-in e-mail.
    /// </summary>
    public const string Pending = "pending";

    /// <summary>
    /// Receives transactional mail only.
    /// </summary>
    public const string Transactional = "transactional";

    /// <summary>
    /// Archived by an admin in Mailchimp.
    /// </summary>
    public const string Archived = "archived";
}
