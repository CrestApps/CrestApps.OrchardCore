using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Taxation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Replaces the no-op checkout tax service with the taxation-aware implementation. It activates
/// automatically whenever both the checkout framework and the taxation framework are enabled, so there is
/// no separate integration feature to switch on. The runtime dependency on taxation stays optional because
/// the wiring only runs when the Taxation feature is present.
/// </summary>
[RequireFeatures(CheckoutConstants.Features.Area, TaxationConstants.Feature.Taxation)]
public sealed class TaxationStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<ICheckoutTaxService>();
        services.AddScoped<ICheckoutTaxService, CheckoutTaxService>();
        services.TryAddScoped<ICheckoutTaxProfileProvider, DefaultCheckoutTaxProfileProvider>();
    }
}
