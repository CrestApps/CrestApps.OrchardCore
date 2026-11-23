using CrestApps.OrchardCore.MailingLists.Models;

namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// A member's page: its state, the evidence of consent and the timeline.
/// </summary>
public class MailingListMemberDetailsViewModel
{
    /// <summary>
    /// Gets or sets the mailing list.
    /// </summary>
    public MailingList MailingList { get; set; }

    /// <summary>
    /// Gets or sets the member.
    /// </summary>
    public MailingListMember Member { get; set; }

    /// <summary>
    /// Gets or sets the name of the list's provider.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets whether the user may change members.
    /// </summary>
    public bool CanManage { get; set; }

    /// <summary>
    /// Gets or sets the question asked before the member's address is erased, which says when an outside provider deletes
    /// the address for good.
    /// </summary>
    public string EraseConfirmation { get; set; }

    /// <summary>
    /// Gets or sets whether the Timeline feature is on, so the member's timeline is shown.
    /// </summary>
    public bool ShowTimeline { get; set; }
}
