using CrestApps.OrchardCore.Orders.Core.Models;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// An email the store sends a customer: who it goes to, what it's about and the culture it's written in.
/// </summary>
public sealed class CommerceEmail
{
    /// <summary>
    /// Gets the kind of email, which names the store's template of it, such as <see cref="OrderEmailKinds.Confirmation"/>.
    /// A kind is registered with <c>AddCommerceEmailKind</c> so the store can write its template. Without a kind, the
    /// email is sent as built, without the store's layout.
    /// </summary>
    public string Kind { get; init; }

    /// <summary>
    /// Gets the address the email goes to.
    /// </summary>
    public string To { get; init; }

    /// <summary>
    /// Gets the order the email is about, which the store's template reads as <c>{{ Order }}</c>, or
    /// <see langword="null"/>.
    /// </summary>
    public Order Order { get; init; }

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
