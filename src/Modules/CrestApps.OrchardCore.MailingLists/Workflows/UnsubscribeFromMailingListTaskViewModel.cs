namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// The editor of the Unsubscribe from Mailing List task.
/// </summary>
public class UnsubscribeFromMailingListTaskViewModel
{
    /// <summary>
    /// Gets or sets the list's id or technical name; empty for every list.
    /// </summary>
    public string List { get; set; }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public string Email { get; set; }
}
