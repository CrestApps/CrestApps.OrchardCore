using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe PaymentMethod API.
/// </summary>
public interface IStripePaymentMethodService
{
    /// <summary>
    /// Retrieves information about a Stripe payment method.
    /// </summary>
    /// <param name="paymentMethodId">The Stripe payment method identifier.</param>
    /// <returns>The payment method information.</returns>
    Task<StripePaymentMethodInfoResponse> GetInformationAsync(string paymentMethodId);
}
