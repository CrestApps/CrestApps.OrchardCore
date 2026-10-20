using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Models;

namespace CrestApps.OrchardCore.Markets.Services;

/// <summary>
/// Makes a checkout's prices include tax when it's in a market whose prices do: the market of its currency that covers
/// where it goes, or else the shopper's own when it pays in that market's currency. The shopper then pays the price
/// they were shown, and the tax is taken out of it.
/// </summary>
public sealed class MarketCheckoutTaxProfileHandler : ICheckoutTaxProfileHandler
{
    private readonly ShopperMarketService _markets;

    public MarketCheckoutTaxProfileHandler(ShopperMarketService markets)
    {
        _markets = markets;
    }

    public async Task BuildingAsync(CheckoutTaxProfile profile, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.PriceType == TaxPriceType.Inclusive || string.IsNullOrEmpty(session?.Currency))
        {
            return;
        }

        var currency = session.Currency;
        var country = profile.Destination?.Country;
        var market = (await _markets.GetMarketsAsync(cancellationToken))
            .FirstOrDefault(candidate => string.Equals(candidate.Currency, currency, StringComparison.OrdinalIgnoreCase) && candidate.Covers(country));

        if (market is null &&
            await _markets.GetCurrentAsync(cancellationToken) is { } current &&
            string.Equals(current.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            market = current;
        }

        if (market?.PricesIncludeTax == true)
        {
            profile.PriceType = TaxPriceType.Inclusive;
        }
    }
}
