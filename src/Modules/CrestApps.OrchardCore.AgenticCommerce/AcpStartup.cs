using CrestApps.OrchardCore.AgenticCommerce.Acp;
using CrestApps.OrchardCore.AgenticCommerce.Orders;
using CrestApps.OrchardCore.Webhooks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers the Agentic Commerce Protocol.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Acp)]
public sealed class AcpStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<AcpCheckoutMapper>();
        services.AddScoped<IAgentOrderPublisher, AcpOrderPublisher>();
        services.AddScoped<IWebhookDeliveryFormatter, AcpOrderWebhookFormatter>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAcp();
}
