using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers what every agent protocol shares: platform sign-in and agents' order updates.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
public sealed class AgentProtocolsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<AgentPlatformAuthenticator>();
        services.AddScoped<AgentOrderViewBuilder>();
        services.AddScoped<AgentOrderUpdates>();
        services.AddScoped<IOrderEventHandler, AgentOrderEventHandler>();
    }
}

/// <summary>
/// Agents' platforms hear when their orders ship and arrive, when the Shipping module is on.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AgenticCommerce)]
[RequireFeatures(ShippingConstants.Features.Shipping)]
public sealed class AgentShipmentsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IShipmentEventHandler, AgentShipmentEventHandler>();
    }
}

/// <summary>
/// Registers the Universal Commerce Protocol.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Ucp)]
public sealed class UcpStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddEgressGuardedHttpClient(UcpProfileClient.HttpClientName);
        services.AddScoped<UcpProfileClient>();
        services.AddScoped<UcpProfileBuilder>();
        services.AddScoped<UcpNegotiator>();
        services.AddScoped<UcpCheckoutMapper>();
        services.AddScoped<UcpOperations>();
        services.AddScoped<IUcpProfileContributor, UcpShoppingProfileContributor>();
        services.AddScoped<IAgentOrderPublisher, UcpOrderPublisher>();
        services.AddScoped<IWebhookDeliveryFormatter, UcpOrderWebhookFormatter>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapUcp();
}
