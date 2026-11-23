namespace CrestApps.OrchardCore.MailingLists.Mailchimp.Models;

/// <summary>
/// A Mailchimp audience webhook, read from its form-encoded body.
/// </summary>
public sealed class MailchimpWebhookEvent
{
    /// <summary>
    /// Gets the event type: <c>subscribe</c>, <c>unsubscribe</c>, <c>profile</c>, <c>upemail</c>, <c>cleaned</c> or
    /// <c>campaign</c>.
    /// </summary>
    public string Type { get; init; }

    /// <summary>
    /// Gets when Mailchimp fired the event (UTC), or <see langword="null"/> when the body had no readable <c>fired_at</c>.
    /// </summary>
    public DateTime? FiredUtc { get; init; }

    /// <summary>
    /// Gets the id of the Mailchimp audience the event is about (<c>data[list_id]</c>).
    /// </summary>
    public string AudienceId { get; init; }

    /// <summary>
    /// Gets the member's address (<c>data[email]</c>).
    /// </summary>
    public string Email { get; init; }

    /// <summary>
    /// Gets the address before an address change (<c>data[old_email]</c>).
    /// </summary>
    public string OldEmail { get; init; }

    /// <summary>
    /// Gets the address after an address change (<c>data[new_email]</c>).
    /// </summary>
    public string NewEmail { get; init; }

    /// <summary>
    /// Gets what Mailchimp did on an unsubscribe (<c>data[action]</c>: <c>unsub</c> or <c>delete</c>).
    /// </summary>
    public string Action { get; init; }

    /// <summary>
    /// Gets why the member left (<c>data[reason]</c>: <c>manual</c> or <c>abuse</c> on an unsubscribe, <c>hard</c> or
    /// <c>abuse</c> on a cleaned address).
    /// </summary>
    public string Reason { get; init; }

    /// <summary>
    /// Gets the member's merge fields (<c>data[merges][TAG]</c>), by tag.
    /// </summary>
    public IReadOnlyDictionary<string, string> Merges { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
