using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Microsoft.Extensions.Logging;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripePaymentMethodService : IStripePaymentMethodService
{
    private readonly ILogger _logger;

    private readonly PaymentMethodService _paymentMethodService;

    public StripePaymentMethodService(StripeClient stripeClient, ILogger<StripePaymentMethodService> logger)
    {
        _logger = logger;
        _paymentMethodService = new PaymentMethodService(stripeClient);
    }

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
