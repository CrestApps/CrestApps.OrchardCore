using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.Extensions.Options;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// What a client needs to confirm a Stripe payment itself, with Stripe's own SDK: the publishable key, whether the
/// account is live, and the amount due in minor units, which Apple Pay and Google Pay sheets show.
/// </summary>
internal sealed class StripeCheckoutClientConfigurationProvider : ICheckoutPaymentClientConfigurationProvider
{
    private readonly IOptionsMonitor<StripeOptions> _options;

    public StripeCheckoutClientConfigurationProvider(IOptionsMonitor<StripeOptions> options)
    {
        _options = options;
    }

    public string ProviderKey
        => StripeConstants.ProcessorKey;

    public Task<IReadOnlyDictionary<string, string>> GetAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["publishableKey"] = options.PublishableKey,
            ["mode"] = options.IsLive ? "live" : "test",
        };

        if (session is not null && session.TryGet<CheckoutInvoice>(out var invoice) && invoice.AmountDue > 0m)
        {
            var currency = string.IsNullOrEmpty(invoice.Currency) ? (session as CheckoutSession)?.Currency : invoice.Currency;

            if (!string.IsNullOrEmpty(currency))
            {
                values["amount"] = StripeCurrency.ToMinorUnits(invoice.AmountDue, currency).ToString(System.Globalization.CultureInfo.InvariantCulture);
                values["currency"] = currency.ToLowerInvariant();
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(values);
    }
}
