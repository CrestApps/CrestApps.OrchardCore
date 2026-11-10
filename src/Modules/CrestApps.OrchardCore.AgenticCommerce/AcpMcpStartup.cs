using CrestApps.OrchardCore.AgenticCommerce.Acp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers ACP's MCP binding.
/// </summary>
[Feature(AgenticCommerceConstants.Features.AcpMcp)]
public sealed class AcpMcpStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<AcpMcpIdempotency>();
        services.AddScoped<IAcpDiscoveryContributor, AcpMcpDiscoveryContributor>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapAcpMcp();
}
