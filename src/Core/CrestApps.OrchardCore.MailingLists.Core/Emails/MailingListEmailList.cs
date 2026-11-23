namespace CrestApps.OrchardCore.MailingLists.Core.Emails;

/// <summary>
/// The list an e-mail is about, as the site's template reads it: <c>{{ List.Name }}</c> and <c>{{ List.Description }}</c>.
/// </summary>
public sealed class MailingListEmailList
{
    /// <summary>
    /// Gets or sets the name people see.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the list sends and how often.
    /// </summary>
    public string Description { get; set; }
}
