using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Server;
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
    {
        services.AddScoped<IUcpProfileContributor, UcpIdentityLinkingProfileContributor>();

        // The OpenID server publishes the authorization server metadata itself (RFC 8414); UCP's scopes and S256-only
        // PKCE are added to it, and the server is made to accept only S256.
        services.TryAdd(UcpAuthorizationServerMetadataHandler.Descriptor.ServiceDescriptor);
        services.Configure<OpenIddictServerOptions>(options => options.Handlers.Add(UcpAuthorizationServerMetadataHandler.Descriptor));
        services.PostConfigure<OpenIddictServerOptions>(UcpAuthorizationServerMetadataHandler.RequireS256);
    }

    /// <inheritdoc />
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // When the OpenID server answers this address, it does so before routing, so this only runs when it doesn't:
        // then the document is built from the server's options, and without a server the answer is 404, so clients
        // fall back to OpenID discovery, as RFC 8414 and UCP expect.
        routes.MapGet("/" + OAuthAuthorizationServerMetadata.Path, (HttpContext httpContext, IOptionsMonitor<OpenIddictServerOptions> options) =>
        {
            var request = httpContext.Request;
            var metadata = OAuthAuthorizationServerMetadata.Build(
                options.CurrentValue,
                $"{request.Scheme}://{request.Host}{request.PathBase}",
                UcpIdentityScopes.Offered);

            if (metadata is null)
            {
                return Results.NotFound();
            }

            httpContext.Response.Headers.CacheControl = "public, max-age=300";

            return Results.Text(metadata.ToJsonString(), "application/json");
        })
            .AllowAnonymous()
            .WithName("UcpAuthorizationServerMetadata");
    }
}
