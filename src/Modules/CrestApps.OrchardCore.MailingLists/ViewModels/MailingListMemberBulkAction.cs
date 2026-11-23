namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// What a bulk action does to the members checked on a list's members page.
/// </summary>
public enum MailingListMemberBulkAction
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None,

    /// <summary>
    /// Unsubscribe the members.
    /// </summary>
    Unsubscribe,

    /// <summary>
    /// Send pending members a new confirmation link.
    /// </summary>
    ResendConfirmation,

    /// <summary>
    /// Queue the state of members whose last change never reached the provider for the provider again.
    /// </summary>
    RetrySync,

    /// <summary>
    /// Erase every membership of the members' addresses, for a privacy request.
    /// </summary>
    Erase,
}
