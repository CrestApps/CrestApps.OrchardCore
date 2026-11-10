using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce;

/// <summary>
/// Registers UCP account linking.
/// </summary>
[Feature(AgenticCommerceConstants.Features.UcpIdentityLinking)]
public sealed class UcpIdentityLinkingStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
        => services.AddScoped<IUcpProfileContributor, UcpIdentityLinkingProfileContributor>();

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // UCP looks for the authorization server's metadata here first (RFC 8414), and falls back to OpenID discovery,
        // which the store's OpenID server serves; this sends it there.
        routes.MapGet("/.well-known/oauth-authorization-server", (HttpContext httpContext) =>
            Results.Redirect($"{httpContext.Request.PathBase}/.well-known/openid-configuration"))
            .AllowAnonymous()
            .WithName("UcpAuthorizationServerMetadata");
    }
}
