using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.PayLater.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.PayLater;

/// <summary>
/// Registers the offline Pay Later payment provider with the checkout framework.
/// </summary>
public sealed class Startup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICheckoutPaymentProvider, PayLaterCheckoutPaymentProvider>();
    }
}
