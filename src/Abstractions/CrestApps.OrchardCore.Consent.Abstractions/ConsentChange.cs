namespace CrestApps.OrchardCore.Consent;

/// <summary>
/// A person opting out of, or back into, a channel at an address.
/// </summary>
public sealed class ConsentChange
{
    /// <summary>
    /// Gets a value indicating whether the person opted out; <see langword="false"/> when they opted back in.
    /// </summary>
    public bool OptedOut { get; init; }

    /// <summary>
    /// Gets where the change came from, such as <c>SmsKeyword</c> or <c>MarketingUnsubscribe</c>, kept with it.
    /// </summary>
    public string Source { get; init; }

    /// <summary>
    /// Gets the kind of the record <see cref="ContactId"/> names, one of <see cref="ConsentContactKinds"/>; a CRM
    /// contact when it's empty.
    /// </summary>
    public string ContactKind { get; init; }

    /// <summary>
    /// Gets the id of the record the person is, or <see langword="null"/> when no record holds the address.
    /// </summary>
    public string ContactId { get; init; }
}
