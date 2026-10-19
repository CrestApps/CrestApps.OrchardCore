using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.PayLater.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PayLater.Services;

/// <summary>
/// Cash on delivery: offered only on checkouts of orders that are delivered, paid to the courier, and recorded by
/// the store when the money comes back.
/// </summary>
public sealed class CashOnDeliveryCheckoutPaymentProvider : OfflineCheckoutPaymentProvider
{
    /// <summary>
    /// The stable processor key of the Cash on delivery method.
    /// </summary>
    public const string ProcessorKey = "cash-on-delivery";

    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public CashOnDeliveryCheckoutPaymentProvider(
        ISiteService siteService,
        IHostEnvironment hostEnvironment,
        IStringLocalizer<CashOnDeliveryCheckoutPaymentProvider> stringLocalizer)
        : base(hostEnvironment)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override string Key => ProcessorKey;

    /// <inheritdoc/>
    public override string DisplayName => S["Cash on delivery"];

    /// <inheritdoc/>
    public override string Description => S["Pay when your order is delivered."];

    /// <inheritdoc/>
    public override Task<DateTime?> GetDueUtcAsync(DateTime now)
        => Task.FromResult<DateTime?>(null);

    /// <inheritdoc/>
    public override async Task<bool> IsAvailableAsync(ICheckoutFlowSession session, CancellationToken cancellationToken = default)
    {
        var settings = await _siteService.GetSettingsAsync<CashOnDeliverySettings>();

        // Only an order with somewhere to deliver it to: the shipping step has saved where it goes.
        return settings.Enabled &&
            session is not null &&
            session.TryGet<CheckoutShippingInfo>(out var shipping) &&
            shipping?.Address is not null;
    }
}
