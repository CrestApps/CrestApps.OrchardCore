namespace CrestApps.OrchardCore.MailingLists.ViewModels;

/// <summary>
/// The member counts on a mailing list's row.
/// </summary>
public class MailingListCountsViewModel
{
    /// <summary>
    /// Gets or sets the list's id.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets how many members are subscribed.
    /// </summary>
    public int Subscribed { get; set; }

    /// <summary>
    /// Gets or sets how many sign-ups wait for a confirmation.
    /// </summary>
    public int Pending { get; set; }

    /// <summary>
    /// Gets or sets how many members' last change did not reach the provider.
    /// </summary>
    public int NotSynced { get; set; }
}
