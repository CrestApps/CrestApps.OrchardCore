namespace CrestApps.OrchardCore.Consent.Unsubscribe;

/// <summary>
/// What an unsubscribe link stops: one mailing list, one campaign's audience, or all marketing e-mail. The kind is an
/// open string; the module that owns the kind registers an <see cref="IUnsubscribeScopeHandler"/> for it.
/// </summary>
public sealed class UnsubscribeScope
{
    /// <summary>
    /// The scope that stops all marketing e-mail to the address.
    /// </summary>
    public static readonly UnsubscribeScope AllMarketing = new() { Kind = UnsubscribeScopeKinds.AllMarketing };

    /// <summary>
    /// Gets the kind of thing the link unsubscribes from, such as <c>MailingList</c>.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the id of the thing within its kind, such as a mailing list id; empty for <see cref="UnsubscribeScopeKinds.AllMarketing"/>.
    /// </summary>
    public string Id { get; init; }
}
