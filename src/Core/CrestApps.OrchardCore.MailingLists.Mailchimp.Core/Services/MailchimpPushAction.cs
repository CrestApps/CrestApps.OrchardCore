namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Services;

/// <summary>
/// What a push of a member to Mailchimp does.
/// </summary>
public enum MailchimpPushAction
{
    /// <summary>
    /// Nothing reaches Mailchimp: a member the site still confirms, or one Mailchimp itself cleaned.
    /// </summary>
    Skip,

    /// <summary>
    /// Creates or updates the member with a status, its merge fields and the list's tags.
    /// </summary>
    Upsert,

    /// <summary>
    /// Marks an existing member unsubscribed, without adding an address Mailchimp does not have.
    /// </summary>
    Unsubscribe,

    /// <summary>
    /// Erases the member permanently.
    /// </summary>
    Erase,
}
