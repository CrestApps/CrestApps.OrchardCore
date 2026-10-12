using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// An optional capability a <see cref="ICheckoutPaymentProvider"/> implements when it can keep the payment method
/// used for a payment and charge it again later without the payer present. Keeping and charging are requested
/// through <see cref="CheckoutPaymentDataKeys"/> on the ordinary begin call, so the money still moves through the
/// checkout engine; this interface only reads back what was kept.
/// </summary>
public interface ICheckoutSavedPaymentMethodProvider
{
    /// <summary>
    /// Gets the key of the provider this capability belongs to. It matches <see cref="ICheckoutPaymentProvider.Key"/>.
    /// </summary>
    string Key { get; }

    /// <summary>
    /// Reads the payment method a settled attempt kept for future use, as the gateway reports it. Returns
    /// <see langword="null"/> when the attempt did not keep one, for example because it was not begun with
    /// <see cref="CheckoutPaymentDataKeys.SavePaymentMethod"/>.
    /// </summary>
    /// <param name="attempt">The settled payment attempt.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<SavedPaymentMethod> GetSavedPaymentMethodAsync(PaymentAttempt attempt, CancellationToken cancellationToken = default);
}
