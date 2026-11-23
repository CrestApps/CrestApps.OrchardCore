namespace CrestApps.OrchardCore.MailingLists.Models;

/// <summary>
/// Well-known ways a member is added to a mailing list. The list is open: a module may use its own source string.
/// </summary>
public static class MailingListMemberSources
{
    /// <summary>
    /// The sign-up widget.
    /// </summary>
    public const string Widget = "Widget";

    /// <summary>
    /// An admin added the member.
    /// </summary>
    public const string Admin = "Admin";

    /// <summary>
    /// A file import.
    /// </summary>
    public const string Import = "Import";

    /// <summary>
    /// The JSON API.
    /// </summary>
    public const string Api = "Api";

    /// <summary>
    /// A workflow task.
    /// </summary>
    public const string Workflow = "Workflow";

    /// <summary>
    /// The list's provider reported the member.
    /// </summary>
    public const string Provider = "Provider";

    /// <summary>
    /// The person used the unsubscribe link of an e-mail, or the e-mail preferences page.
    /// </summary>
    public const string UnsubscribeLink = "UnsubscribeLink";

    /// <summary>
    /// A consent change recorded elsewhere (a "stop all marketing", a CRM do-not-email) reached the list.
    /// </summary>
    public const string Consent = "Consent";

    /// <summary>
    /// The member's state aged out, such as a sign-up never confirmed.
    /// </summary>
    public const string Retention = "Retention";
}
