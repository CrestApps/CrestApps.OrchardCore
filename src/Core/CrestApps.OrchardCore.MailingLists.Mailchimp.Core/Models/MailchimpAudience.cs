namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// A Mailchimp audience (list), as the audience picker shows it.
/// </summary>
public sealed class MailchimpAudience
{
    /// <summary>
    /// Gets the audience's id.
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the audience's name.
    /// </summary>
    public string Name { get; init; }
}
