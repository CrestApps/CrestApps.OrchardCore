namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// A message for a buyer, such as a receipt, a payment reminder or a notice about their payment plan.
/// </summary>
public sealed class CustomerOwnerMessage
{
    /// <summary>
    /// Gets the kind of message, such as <c>payment-receipt</c>, which names the store's template of it. A message with a
    /// kind passes through the store's templates of its emails before it's sent; without one, it goes as built.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the records the message is about, by the name the store's template reads them under.
    /// </summary>
    public IReadOnlyDictionary<string, object> Models { get; init; } = new Dictionary<string, object>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the message's own values the store's template can use, such as the link to pay, by name.
    /// </summary>
    public IReadOnlyDictionary<string, string> Values { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the subject line.
    /// </summary>
    public string Subject { get; init; }

    /// <summary>
    /// Gets the short summary a notification list shows. The text body is used when it is empty.
    /// </summary>
    public string Summary { get; init; }

    /// <summary>
    /// Gets the plain-text body.
    /// </summary>
    public string TextBody { get; init; }

    /// <summary>
    /// Gets the HTML body, or <see langword="null"/> for a plain-text message.
    /// </summary>
    public string HtmlBody { get; init; }
}
