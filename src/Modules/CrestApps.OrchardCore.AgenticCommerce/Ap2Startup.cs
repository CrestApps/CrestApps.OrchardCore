using CrestApps.OrchardCore.AgenticCommerce.Ap2;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers UCP's AP2 mandates extension.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Ap2)]
public sealed class Ap2Startup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUcpProfileContributor, Ap2ProfileContributor>();
        services.AddScoped<IUcpCheckoutExtension, Ap2CheckoutExtension>();
    }
}
