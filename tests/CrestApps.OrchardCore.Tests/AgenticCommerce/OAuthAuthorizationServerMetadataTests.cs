using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Ucp;
using OpenIddict.Server;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// The store serves RFC 8414 authorization server metadata built from its OpenID server's options, with what UCP
/// account linking requires (PKCE S256, the iss parameter, UCP's scopes), and no document at all when there is no
/// server, so clients fall back to OpenID discovery.
/// </summary>
public sealed class OAuthAuthorizationServerMetadataTests
{
    [Fact]
    public void Build_DescribesTheServer_FromItsOptions()
    {
        // Arrange
        var options = CreateOptions();

        // Act
        var metadata = OAuthAuthorizationServerMetadata.Build(options, "https://shop.example/store", UcpIdentityScopes.Offered);

        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("https://shop.example/store/", metadata["issuer"]!.GetValue<string>());
        Assert.Equal("https://shop.example/store/connect/authorize", metadata["authorization_endpoint"]!.GetValue<string>());
        Assert.Equal("https://shop.example/store/connect/token", metadata["token_endpoint"]!.GetValue<string>());
        Assert.Equal("https://shop.example/store/connect/revoke", metadata["revocation_endpoint"]!.GetValue<string>());
        Assert.Equal("https://shop.example/store/.well-known/jwks", metadata["jwks_uri"]!.GetValue<string>());
        Assert.Equal(new[] { "code" }, Strings(metadata["response_types_supported"]));
        Assert.Equal(new[] { "authorization_code", "client_credentials", "refresh_token" }, Strings(metadata["grant_types_supported"]));
        Assert.Equal(new[] { "S256" }, Strings(metadata["code_challenge_methods_supported"]));
        Assert.Equal(new[] { "client_secret_basic", "client_secret_post" }, Strings(metadata["token_endpoint_auth_methods_supported"]));
        Assert.Equal(new[] { "client_secret_basic", "client_secret_post" }, Strings(metadata["revocation_endpoint_auth_methods_supported"]));
        Assert.True(metadata["authorization_response_iss_parameter_supported"]!.GetValue<bool>());
    }

    [Fact]
    public void Build_ListsTheServersScopesAndUcpsScopes()
    {
        var metadata = OAuthAuthorizationServerMetadata.Build(CreateOptions(), "https://shop.example", UcpIdentityScopes.Offered);

        var scopes = Strings(metadata!["scopes_supported"]);

        Assert.Contains("openid", scopes);
        Assert.Contains("offline_access", scopes);
        Assert.Contains(UcpIdentityScopes.CheckoutManage, scopes);
        Assert.Contains(UcpIdentityScopes.OrderRead, scopes);
        Assert.Equal(scopes.Distinct().Count(), scopes.Count);
    }

    [Fact]
    public void Build_UsesTheConfiguredIssuer_AndKeepsAbsoluteEndpoints()
    {
        // Arrange
        var options = CreateOptions();
        options.Issuer = new Uri("https://login.example/tenant/");
        options.TokenEndpointUris.Clear();
        options.TokenEndpointUris.Add(new Uri("https://tokens.example/connect/token"));

        // Act
        var metadata = OAuthAuthorizationServerMetadata.Build(options, "https://shop.example", []);

        // Assert
        Assert.Equal("https://login.example/tenant/", metadata!["issuer"]!.GetValue<string>());
        Assert.Equal("https://login.example/tenant/connect/authorize", metadata["authorization_endpoint"]!.GetValue<string>());
        Assert.Equal("https://tokens.example/connect/token", metadata["token_endpoint"]!.GetValue<string>());
    }

    [Fact]
    public void Build_LeavesOutWhatTheServerDoesNotServe()
    {
        // Arrange
        var options = CreateOptions();
        options.RevocationEndpointUris.Clear();

        // Act
        var metadata = OAuthAuthorizationServerMetadata.Build(options, "https://shop.example", []);

        // Assert
        Assert.False(metadata!.ContainsKey("revocation_endpoint"));
        Assert.False(metadata.ContainsKey("revocation_endpoint_auth_methods_supported"));
        Assert.False(metadata.ContainsKey("introspection_endpoint"));
    }

    [Fact]
    public void Build_ReturnsNothing_WhenThereIsNoServer()
    {
        Assert.Null(OAuthAuthorizationServerMetadata.Build(new OpenIddictServerOptions(), "https://shop.example", UcpIdentityScopes.Offered));
        Assert.Null(OAuthAuthorizationServerMetadata.Build(null, "https://shop.example", UcpIdentityScopes.Offered));
    }

    [Fact]
    public async Task Handler_AddsUcpScopes_AndOffersOnlyS256_InTheServersOwnDocument()
    {
        // Arrange
        var context = new OpenIddictServerEvents.HandleConfigurationRequestContext(new OpenIddictServerTransaction());
        context.Scopes.UnionWith(["openid", "profile"]);
        context.CodeChallengeMethods.UnionWith(["plain", "S256"]);

        // Act
        await new UcpAuthorizationServerMetadataHandler().HandleAsync(context);

        // Assert
        Assert.Contains(UcpIdentityScopes.CheckoutManage, context.Scopes);
        Assert.Contains(UcpIdentityScopes.OrderRead, context.Scopes);
        Assert.Contains("openid", context.Scopes);
        Assert.Equal(new[] { "S256" }, context.CodeChallengeMethods);
    }

    [Fact]
    public void RequireS256_MakesTheServerRefusePlainChallenges()
    {
        var options = CreateOptions();

        UcpAuthorizationServerMetadataHandler.RequireS256(options);

        Assert.Equal(new[] { "S256" }, options.CodeChallengeMethods);
    }

    private static OpenIddictServerOptions CreateOptions()
    {
        var options = new OpenIddictServerOptions();

        options.AuthorizationEndpointUris.Add(new Uri("connect/authorize", UriKind.Relative));
        options.TokenEndpointUris.Add(new Uri("connect/token", UriKind.Relative));
        options.RevocationEndpointUris.Add(new Uri("connect/revoke", UriKind.Relative));
        options.JsonWebKeySetEndpointUris.Add(new Uri("/.well-known/jwks", UriKind.Relative));
        options.GrantTypes.UnionWith(["authorization_code", "refresh_token", "client_credentials"]);
        options.Scopes.UnionWith(["openid", "offline_access"]);
        options.ClientAuthenticationMethods.Clear();
        options.ClientAuthenticationMethods.UnionWith(["client_secret_post", "client_secret_basic"]);
        options.CodeChallengeMethods.UnionWith(["S256", "plain"]);
        options.ResponseTypes.UnionWith(["code", "token"]);

        return options;
    }

    private static List<string> Strings(JsonNode node)
        => node!.AsArray().Select(entry => entry!.GetValue<string>()).ToList();
}
