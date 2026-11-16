using System.Security.Claims;
using CrestApps.OrchardCore.Learning.Lti.Core.Launch;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Services;

/// <summary>
/// Validates the <c>private_key_jwt</c> client assertions of registered external tools at the OpenID server's token
/// endpoint, in the shape the 1EdTech Security Framework (4.1) describes and LTI tools send: <c>typ</c> <c>JWT</c> (or
/// none), <c>aud</c> the token endpoint URL (or the issuer), <c>iss</c> and <c>sub</c> the client id, <c>exp</c>, and a
/// <c>jti</c> not seen before, signed with one of the tool's keys (its JWKS URL, or the key set of its OpenID
/// application). OpenIddict 7 accepts only <c>client-authentication+jwt</c> with the issuer as audience; its own
/// validation skips a token whose principal is already set, so this handler runs just before it and sets the principal
/// for tools only. Every other client assertion is left to OpenIddict. Everything after it (client type, grant type and
/// scope permissions, the access token) is OpenIddict's and Orchard Core's.
/// </summary>
public sealed class LtiClientAssertionHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenContext>
{
    private const string ClientAssertionTokenType = "urn:openiddict:params:oauth:token-type:client_assertion";

    private readonly ILtiToolDirectory _tools;
    private readonly ILtiPlatform _platform;
    private readonly LtiNonceStore _nonces;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiClientAssertionHandler"/> class.
    /// </summary>
    /// <param name="tools">The tool directory.</param>
    /// <param name="platform">The platform.</param>
    /// <param name="nonces">The nonce store, for the token ids.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public LtiClientAssertionHandler(
        ILtiToolDirectory tools,
        ILtiPlatform platform,
        LtiNonceStore nonces,
        IClock clock,
        ILogger<LtiClientAssertionHandler> logger)
    {
        _tools = tools;
        _platform = platform;
        _nonces = nonces;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Gets the handler's descriptor: scoped, just before OpenIddict validates the token itself.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; } = OpenIddictServerHandlerDescriptor.CreateBuilder<OpenIddictServerEvents.ValidateTokenContext>()
        .UseScopedHandler<LtiClientAssertionHandler>()
        .SetOrder(OpenIddictServerHandlers.Protection.ValidateIdentityModelToken.Descriptor.Order - 500)
        .SetType(OpenIddictServerHandlerType.Custom)
        .Build();

    /// <inheritdoc />
    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Principal is not null ||
            context.ValidTokenTypes.Count != 1 ||
            !context.ValidTokenTypes.Contains(ClientAssertionTokenType) ||
            string.IsNullOrEmpty(context.Token))
        {
            return;
        }

        JsonWebToken unverified;

        try
        {
            unverified = new JsonWebToken(context.Token);
        }
        catch (ArgumentException)
        {
            return;
        }

        var tool = await _tools.FindByClientIdAsync(unverified.Issuer);

        if (tool is null)
        {
            return;
        }

        if (!tool.Enabled)
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "The external tool is turned off.");

            return;
        }

        var platform = await _platform.GetAsync();
        var keys = await _tools.GetSigningKeysAsync(tool);

        if (!string.IsNullOrEmpty(unverified.Kid) && !keys.Any(key => string.Equals(key.KeyId, unverified.Kid, StringComparison.Ordinal)))
        {
            keys = await _tools.GetSigningKeysAsync(tool, refresh: true);
        }

        var now = _clock.UtcNow;
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(context.Token, new TokenValidationParameters
        {
            ValidIssuer = tool.ClientId,
            ValidAudiences = [platform.TokenEndpoint, platform.Issuer, platform.Issuer + "/"],
            IssuerSigningKeys = keys,
            TryAllIssuerSigningKeys = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.RsaSha384, SecurityAlgorithms.RsaSha512],
            ValidTypes = ["JWT", "client-authentication+jwt", "application/client-authentication+jwt"],
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            LifetimeValidator = (notBefore, expires, _, _) =>
                expires is { } expiry &&
                DateTime.SpecifyKind(expiry, DateTimeKind.Utc) + LtiTokens.ClockSkew >= now &&
                (notBefore is null || DateTime.SpecifyKind(notBefore.Value, DateTimeKind.Utc) - LtiTokens.ClockSkew <= now),
        });

        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            _logger.LogInformation("A client assertion of an external tool was refused: it is not signed by the tool's key, or its issuer, audience, type or times are not valid.");
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "The client assertion is not valid.");

            return;
        }

        if (!string.Equals(token.Subject, tool.ClientId, StringComparison.Ordinal))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "The client assertion's sub must be the client id.");

            return;
        }

        if (!await _nonces.TryUseAsync("client-assertion:" + tool.ClientId, token.Id))
        {
            context.Reject(OpenIddictConstants.Errors.InvalidClient, "The client assertion's jti is missing or was used before.");

            return;
        }

        // OpenIddict checks the audience against the issuer only; the token endpoint URL, which LTI tools send, is valid too.
        foreach (var audience in token.Audiences)
        {
            context.ValidAudiences.Add(audience);
        }

        context.Principal = new ClaimsPrincipal(result.ClaimsIdentity).SetTokenType(ClientAssertionTokenType);
    }
}
