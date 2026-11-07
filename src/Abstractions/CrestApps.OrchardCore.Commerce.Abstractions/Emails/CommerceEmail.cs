namespace CrestApps.OrchardCore.Commerce.Emails;

/// <summary>
/// An email the store sends a customer: its kind, who it goes to, the records it's about and the culture it's written in.
/// </summary>
public sealed class CommerceEmail
{
    /// <summary>
    /// Gets the kind of email, which names the store's template of it, such as <c>order-confirmation</c>. A kind is
    /// registered with <c>AddCommerceEmailKind</c> so the store can write its template. Without a kind, the email is sent
    /// as built, without the store's layout.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the address the email goes to.
    /// </summary>
    public string To { get; init; }

    /// <summary>
    /// Gets the records the email is about, by the name the store's template reads them under, such as the order as
    /// <c>{{ Order }}</c>. Each is read as plain values, such as <c>{{ Order.OrderNumber }}</c>.
    /// </summary>
    public IReadOnlyDictionary<string, object> Models { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the culture the email is written in, such as the one the buyer shopped in, or <see langword="null"/> to keep
    /// the current one.
    /// </summary>
    public string Culture { get; init; }

    /// <summary>
    /// Gets what the email is about, such as an order number, for the log when it can't be sent.
    /// </summary>
    public string Reference { get; init; }
}
