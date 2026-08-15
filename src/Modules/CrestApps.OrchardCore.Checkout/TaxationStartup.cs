using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Replaces the no-op checkout tax service with the taxation-aware implementation. This runs only when the
/// Taxation feature is enabled, keeping the runtime dependency on taxation optional.
/// </summary>
[RequireFeatures(CheckoutConstants.Features.Taxation)]
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
