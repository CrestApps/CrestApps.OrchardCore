using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers UCP's Agent2Agent (A2A) binding.
/// </summary>
[Feature(AgenticCommerceConstants.Features.UcpA2a)]
public sealed class UcpA2aStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IUcpProfileContributor, UcpA2aProfileContributor>();
        services.AddScoped<UcpA2aMessageHandler>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapUcpA2a();
}
