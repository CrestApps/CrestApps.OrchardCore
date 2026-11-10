using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers UCP's catalog search and lookup and the cart.
/// </summary>
[Feature(AgenticCommerceConstants.Features.UcpCatalog)]
public sealed class UcpCatalogStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<UcpCatalog>();
        services.AddScoped<IUcpProfileContributor, UcpCatalogProfileContributor>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapUcpCatalog();
}
