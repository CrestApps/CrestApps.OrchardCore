namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// A page of audience members, as the members endpoint returns it.
/// </summary>
public sealed class MailchimpMemberPage
{
    /// <summary>
    /// Gets the members on the page.
    /// </summary>
    public IReadOnlyList<MailchimpMember> Members { get; init; } = [];

    /// <summary>
    /// Gets how many members match the query on every page.
    /// </summary>
    public int TotalItems { get; init; }
}
