namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// How a sign-up to a mailing list is confirmed.
/// </summary>
public enum MailingListConfirmation
{
    /// <summary>
    /// Single opt-in: the person is subscribed at once.
    /// </summary>
    None = 0,

    /// <summary>
    /// Double opt-in: the site e-mails a confirmation link and subscribes the person when they confirm.
    /// </summary>
    Email = 1,

    /// <summary>
    /// The list's provider sends its own confirmation e-mail and reports the confirmation back.
    /// </summary>
    Provider = 2,
}
