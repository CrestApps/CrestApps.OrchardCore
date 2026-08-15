using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Checkout;

/// <summary>
/// Registers the Pay Later checkout payment provider.
/// </summary>
[Feature(CheckoutConstants.Features.PayLater)]
public sealed class PayLaterStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutPaymentProvider, PayLaterCheckoutPaymentProvider>();
    }
}
