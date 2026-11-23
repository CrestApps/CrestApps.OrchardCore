namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// A member of a Mailchimp audience, as the members endpoint returns it.
/// </summary>
public sealed class MailchimpMember
{
    /// <summary>
    /// Gets the member's id (the subscriber hash).
    /// </summary>
    public string Id { get; init; }

    /// <summary>
    /// Gets the member's address.
    /// </summary>
    public string EmailAddress { get; init; }

    /// <summary>
    /// Gets the member's status: <c>subscribed</c>, <c>unsubscribed</c>, <c>cleaned</c>, <c>pending</c>,
    /// <c>transactional</c> or <c>archived</c>.
    /// </summary>
    public string Status { get; init; }

    /// <summary>
    /// Gets when the member last changed (UTC), or <see langword="null"/> when Mailchimp did not say.
    /// </summary>
    public DateTime? LastChangedUtc { get; init; }

    /// <summary>
    /// Gets the member's merge fields that hold text, by tag.
    /// </summary>
    public IReadOnlyDictionary<string, string> MergeFields { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
