namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Removes a buyer's personal data from the checkout sessions behind something they paid for, such as an order or a
/// transaction, when that thing is anonymized at the buyer's request or after a retention window. Every session started
/// for it is anonymized, not only the newest, because each earlier attempt holds what the buyer typed too.
/// </summary>
/// <remarks>
/// A module or application with its own checkout source calls this with its own reference type when it erases a person,
/// so the checkout decides what is personal on a session and what must stay for the store's accounts.
/// </remarks>
public interface ICheckoutSessionAnonymizer
{
    /// <summary>
    /// Anonymizes the session with the given id, unless it is already anonymized.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when the session existed and was anonymized now.</returns>
    Task<bool> AnonymizeAsync(string sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Anonymizes every session started for the supplied reference that is not already anonymized.
    /// </summary>
    /// <param name="referenceType">The reference type, for example <see cref="CheckoutReferenceTypes.Order"/>.</param>
    /// <param name="referenceId">The identifier of the referenced thing (for an order, its item id).</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>The number of sessions anonymized now.</returns>
    Task<int> AnonymizeByReferenceAsync(string referenceType, string referenceId, CancellationToken cancellationToken = default);
}
