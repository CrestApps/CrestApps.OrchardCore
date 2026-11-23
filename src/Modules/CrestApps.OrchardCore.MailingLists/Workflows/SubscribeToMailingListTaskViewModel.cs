namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// The editor of the Subscribe to Mailing List task.
/// </summary>
public class SubscribeToMailingListTaskViewModel
{
    /// <summary>
    /// Gets or sets the list's id or technical name.
    /// </summary>
    public string List { get; set; }

    /// <summary>
    /// Gets or sets the address.
    /// </summary>
    public string Email { get; set; }

    /// <summary>
    /// Gets or sets the person's name.
    /// </summary>
    public string PersonName { get; set; }
}
