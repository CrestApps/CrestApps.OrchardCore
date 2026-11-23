using CrestApps.OrchardCore.MailingLists.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// A list's members page.
/// </summary>
public class MailingListMembersIndexViewModel
{
    /// <summary>
    /// Gets or sets the mailing list.
    /// </summary>
    [BindNever]
    public MailingList MailingList { get; set; }

    /// <summary>
    /// Gets or sets the search, filters and bulk action.
    /// </summary>
    public MailingListMembersOptions Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the members, for the admin list.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets whether the user may change members.
    /// </summary>
    [BindNever]
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the question asked before members are erased, which says when an outside provider deletes the
    /// addresses for good.
    /// </summary>
    [BindNever]
    public string EraseConfirmation { get; set; }
}
