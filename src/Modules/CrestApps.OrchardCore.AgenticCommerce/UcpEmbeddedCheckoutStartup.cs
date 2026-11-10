using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers UCP embedded checkout (ECP). It stays off until the store allows embedded checkout and a platform lists the
/// origins that may frame it.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Ucp)]
public sealed class UcpEmbeddedCheckoutStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<EmbeddedCheckoutPolicy>();
        services.AddScoped<EmbeddedCheckoutContextCookie>();
        services.AddScoped<EmbeddedCheckoutService>();
        services.AddScoped<IUcpProfileContributor, UcpEmbeddedProfileContributor>();
        services.AddScoped<IUcpCheckoutExtension, UcpEmbeddedCheckoutExtension>();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, AgenticCommerceResourceManagementOptionsConfiguration>();
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => app.UseMiddleware<EmbeddedCheckoutMiddleware>();
}
