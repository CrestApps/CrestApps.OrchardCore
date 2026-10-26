using CrestApps.OrchardCore.Checkout.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// The payment methods each customer kept for charges made without them present, whatever provider holds them. A
/// checkout that keeps the card (<see cref="CheckoutSession.SavePaymentMethod"/>), such as an installment plan's first
/// payment or a booking deposit, adds the card here when it completes, so a module can later find the customer's cards
/// and charge one, for example to renew a subscription charged to the card on file.
/// </summary>
/// <remarks>
/// Every lookup that takes an owner only returns that owner's payment methods, so an identifier sent from a browser can
/// never reach someone else's card. The store keeps only the provider's references and what describes the card to a
/// person; the card itself stays with the provider.
/// </remarks>
public interface ISavedPaymentMethodStore
{
    /// <summary>
    /// Lists the owner's kept payment methods, the default first and then the newest first.
    /// </summary>
    /// <param name="ownerId">The identifier of the user the payment methods belong to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<CustomerPaymentMethod>> ListAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a kept payment method by its identifier, whoever it belongs to, or <see langword="null"/> when there is
    /// none. Server code that charges it must check <see cref="CustomerPaymentMethod.OwnerId"/> itself.
    /// </summary>
    /// <param name="id">The identifier of the kept payment method.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CustomerPaymentMethod> FindByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one of the owner's kept payment methods, or <see langword="null"/> when the owner has none with that
    /// identifier.
    /// </summary>
    /// <param name="ownerId">The identifier of the user the payment method belongs to.</param>
    /// <param name="id">The identifier of the kept payment method.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CustomerPaymentMethod> FindAsync(string ownerId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the owner's default payment method: the one marked default, or else the newest one that has not expired.
    /// Returns <see langword="null"/> when the owner kept none that can still be charged.
    /// </summary>
    /// <param name="ownerId">The identifier of the user the payment methods belong to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<CustomerPaymentMethod> GetDefaultAsync(string ownerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Keeps a payment method for the owner. A payment method the owner already kept with the same provider and
    /// reference is updated rather than added twice. The owner's first payment method becomes their default.
    /// </summary>
    /// <param name="ownerId">The identifier of the user the payment method belongs to.</param>
    /// <param name="paymentMethod">The payment method as the provider reported it.</param>
    /// <param name="sourceSessionId">The checkout session that kept it, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The kept payment method.</returns>
    Task<CustomerPaymentMethod> SaveAsync(string ownerId, SavedPaymentMethod paymentMethod, string sourceSessionId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes one of the owner's payment methods their default. Returns <see langword="false"/> when the owner has none
    /// with that identifier.
    /// </summary>
    /// <param name="ownerId">The identifier of the user the payment method belongs to.</param>
    /// <param name="id">The identifier of the kept payment method.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> SetDefaultAsync(string ownerId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that a payment method was charged successfully without the owner present.
    /// </summary>
    /// <param name="id">The identifier of the kept payment method.</param>
    /// <param name="chargedUtc">When it was charged.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task MarkChargedAsync(string id, DateTime chargedUtc, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets one of the owner's payment methods, so it is no longer offered or charged from this store. When it was
    /// the default, the newest remaining one becomes the default. The provider is not asked to delete the card, and a
    /// record that keeps its own copy, such as an installment plan, keeps charging it. Returns
    /// <see langword="false"/> when the owner has none with that identifier.
    /// </summary>
    /// <param name="ownerId">The identifier of the user the payment method belongs to.</param>
    /// <param name="id">The identifier of the kept payment method.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> RemoveAsync(string ownerId, string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets every payment method the owner kept, such as when their personal data is erased. Returns how many were
    /// removed.
    /// </summary>
    /// <param name="ownerId">The identifier of the user the payment methods belong to.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<int> RemoveAllAsync(string ownerId, CancellationToken cancellationToken = default);
}
