using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Adds what UCP account linking needs to the metadata the store's OpenID server publishes itself, at
/// <c>/.well-known/oauth-authorization-server</c> (RFC 8414) and <c>/.well-known/openid-configuration</c>: UCP's
/// scopes in <c>scopes_supported</c>, and only <c>S256</c> in <c>code_challenge_methods_supported</c>.
/// </summary>
/// <remarks>
/// OpenIddict answers both addresses before routing, from the options Orchard Core's OpenID server feature gives it, so
/// the store adds to that document instead of serving another one. The server itself is set to accept only S256 PKCE
/// while UCP account linking is on (see <see cref="RequireS256"/>), so the document says what the server does.
/// </remarks>
public sealed class UcpAuthorizationServerMetadataHandler : IOpenIddictServerHandler<OpenIddictServerEvents.HandleConfigurationRequestContext>
{
    /// <summary>
    /// Gets the handler's descriptor, after OpenIddict's own metadata handlers.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } =
        OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.HandleConfigurationRequestContext>()
            .UseSingletonHandler<UcpAuthorizationServerMetadataHandler>()
            .SetOrder(OpenIddictServerHandlers.Discovery.AttachAdditionalMetadata.Descriptor.Order + 1_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc />
    public ValueTask HandleAsync(OpenIddictServerEvents.HandleConfigurationRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Scopes.UnionWith(UcpIdentityScopes.Offered);
        context.CodeChallengeMethods.Remove(OpenIddictConstants.CodeChallengeMethods.Plain);

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Makes the OpenID server accept only S256 PKCE challenges, as UCP account linking requires.
    /// </summary>
    /// <param name="options">The OpenID server's options.</param>
    public static void RequireS256(OpenIddictServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.CodeChallengeMethods.Remove(OpenIddictConstants.CodeChallengeMethods.Plain);
    }
}
