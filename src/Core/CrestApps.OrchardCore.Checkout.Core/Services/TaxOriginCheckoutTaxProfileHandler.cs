using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// Puts where the store sells from (Settings > Commerce > Tax origin) on the checkout's tax profile.
/// </summary>
public sealed class TaxOriginCheckoutTaxProfileHandler : ICheckoutTaxProfileHandler
{
    private readonly ISiteService _siteService;

    public TaxOriginCheckoutTaxProfileHandler(ISiteService siteService)
    {
        _siteService = siteService;
    }

    public async Task BuildingAsync(CheckoutTaxProfile profile, ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        profile.Origin ??= (await _siteService.GetSettingsAsync<TaxOriginSettings>()).Origin;
    }
}
