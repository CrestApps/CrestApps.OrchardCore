using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.MailingLists;

/// <summary>
/// The permissions of the mailing lists module.
/// </summary>
public static class MailingListsPermissions
{
    /// <summary>
    /// Create, change and delete mailing lists.
    /// </summary>
    public static readonly Permission ManageMailingLists = new("ManageMailingLists", LocalizationSource.Create("Manage mailing lists", typeof(MailingListsPermissions)));

    /// <summary>
    /// Add, unsubscribe and erase the members of mailing lists.
    /// </summary>
    public static readonly Permission ManageMailingListMembers = new("ManageMailingListMembers", LocalizationSource.Create("Manage the members of mailing lists", typeof(MailingListsPermissions)), [ManageMailingLists]);

    /// <summary>
    /// See the members of mailing lists and the evidence of their consent.
    /// </summary>
    public static readonly Permission ViewMailingListMembers = new("ViewMailingListMembers", LocalizationSource.Create("View the members of mailing lists", typeof(MailingListsPermissions)), [ManageMailingListMembers]);

    /// <summary>
    /// Change the mailing lists settings: the e-mail templates and the provider connections.
    /// </summary>
    public static readonly Permission ManageMailingListSettings = new("ManageMailingListSettings", LocalizationSource.Create("Manage the mailing lists settings", typeof(MailingListsPermissions)));
}
