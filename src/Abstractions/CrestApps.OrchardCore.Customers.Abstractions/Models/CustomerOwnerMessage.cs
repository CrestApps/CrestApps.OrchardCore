namespace CrestApps.OrchardCore.Customers.Models;

/// <summary>
/// A message for a buyer, such as a receipt, a payment reminder or a notice about their payment plan.
/// </summary>
public sealed class CustomerOwnerMessage
{
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
