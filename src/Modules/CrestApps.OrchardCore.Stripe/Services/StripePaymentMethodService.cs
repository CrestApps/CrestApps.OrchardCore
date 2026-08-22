using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Implements Stripe PaymentMethod lookup operations.
/// </summary>
public sealed class StripePaymentMethodService : IStripePaymentMethodService
{
    private readonly ILogger _logger;

    private readonly PaymentMethodService _paymentMethodService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentMethodService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to call the Stripe API.</param>
    /// <param name="logger">The logger used to record Stripe payment method lookup failures.</param>
    public StripePaymentMethodService(StripeClient stripeClient, ILogger<StripePaymentMethodService> logger)
    {
        _logger = logger;
        _paymentMethodService = new PaymentMethodService(stripeClient);
    }

    /// <summary>
    /// Retrieves card information for a Stripe payment method.
    /// </summary>
    /// <param name="paymentMethodId">The Stripe payment method identifier.</param>
    /// <returns>The payment method card information, or <see langword="null"/> when it cannot be loaded.</returns>
    public async Task<StripePaymentMethodInfoResponse> GetInformationAsync(string paymentMethodId)
    {
        ArgumentException.ThrowIfNullOrEmpty(paymentMethodId);

        try
        {
            var paymentMethod = await _paymentMethodService.GetAsync(paymentMethodId);

            if (paymentMethod?.Card != null)
            {
                return new StripePaymentMethodInfoResponse
                {
                    Card = new StripePaymentCardInfoResponse
                    {
                        Brand = paymentMethod.Card.Brand,
                        Country = paymentMethod.Card.Country,
                        DisplayBrand = paymentMethod.Card.DisplayBrand,
                        ExpirationMonth = paymentMethod.Card.ExpMonth,
                        ExpirationYear = paymentMethod.Card.ExpYear,
                        Fingerprint = paymentMethod.Card.Fingerprint,
                        Issuer = paymentMethod.Card.Issuer,
                        LastFour = paymentMethod.Card.Last4,
                    },
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to get card info from Stripe.");
        }

        return null;
    }
}
