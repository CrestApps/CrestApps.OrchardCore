using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The search, filters and bulk action of a list's members page.
/// </summary>
public class MailingListMembersOptions
{
    /// <summary>
    /// Gets or sets a part of the address to look for.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the state to show (<c>Pending</c>, <c>Subscribed</c>, <c>Unsubscribed</c>, <c>Cleaned</c> or
    /// <c>NotSynced</c>), or empty for every member.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the source the members joined through (see <c>MailingListMemberSources</c>), or empty for every source.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the bulk action to run on the checked members.
    /// </summary>
    public MailingListMemberBulkAction BulkAction { get; set; }

    /// <summary>
    /// Gets or sets the bulk actions the page offers.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> BulkActions { get; set; } = [];
}
