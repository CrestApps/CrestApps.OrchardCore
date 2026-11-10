using System.Security.Claims;
using System.Text.Json.Nodes;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.AgenticCommerce.Endpoints;
using CrestApps.OrchardCore.AgenticCommerce.ViewModels;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using OpenIddict.Validation;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Runs UCP embedded checkout (ECP): decides whether a hand-off may be shown in the platform's page, prepares the page
/// that speaks the protocol to the platform, reads the checkout for it, and checks the platform's credential.
/// </summary>
/// <remarks>
/// The page the platform frames is the store's own: a small page holds the protocol channel for the whole session and
/// shows the store's checkout in a frame of its own, so the buyer moves through the checkout's pages without the
/// channel being set up again. Only the platform that owns the checkout may frame it, from the origins it lists.
/// </remarks>
public sealed class EmbeddedCheckoutService
{
    private static readonly TimeSpan _tokenLifetime = EmbeddedCheckoutContextCookie.Lifetime;

    // The capabilities an embedded checkout's state is written with: the checkout and the extensions a buyer sees.
    private static readonly HashSet<string> _stateCapabilities = new(StringComparer.Ordinal)
    {
        UcpConstants.Capabilities.Checkout,
        UcpConstants.Capabilities.Fulfillment,
        UcpConstants.Capabilities.Discount,
        UcpConstants.Capabilities.BuyerConsent,
    };

    private readonly EmbeddedCheckoutPolicy _policy;
    private readonly ISiteService _siteService;
    private readonly IAgentPlatformStore _platforms;
    private readonly IAgentCheckoutStore _checkouts;
    private readonly IAgentCheckoutService _checkoutService;
    private readonly UcpCheckoutMapper _mapper;
    private readonly UcpProfileBuilder _profiles;
    private readonly AgentSiteUrls _urls;
    private readonly SignedTokenService _signedTokens;
    private readonly EmbeddedCheckoutContextCookie _cookie;
    private readonly LinkGenerator _linkGenerator;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCheckoutService"/> class.
    /// </summary>
    /// <param name="policy">Who may frame embedded checkout, and where.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="platforms">The agent platforms.</param>
    /// <param name="checkouts">The agent checkouts.</param>
    /// <param name="checkoutService">The agent checkout.</param>
    /// <param name="mapper">The UCP checkout mapper.</param>
    /// <param name="profiles">The store's UCP profile.</param>
    /// <param name="urls">The agent addresses.</param>
    /// <param name="signedTokens">The signed token service.</param>
    /// <param name="cookie">The embedded checkout cookie.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public EmbeddedCheckoutService(
        EmbeddedCheckoutPolicy policy,
        ISiteService siteService,
        IAgentPlatformStore platforms,
        IAgentCheckoutStore checkouts,
        IAgentCheckoutService checkoutService,
        UcpCheckoutMapper mapper,
        UcpProfileBuilder profiles,
        AgentSiteUrls urls,
        SignedTokenService signedTokens,
        EmbeddedCheckoutContextCookie cookie,
        LinkGenerator linkGenerator,
        IClock clock,
        ILogger<EmbeddedCheckoutService> logger)
    {
        _policy = policy;
        _siteService = siteService;
        _platforms = platforms;
        _checkouts = checkouts;
        _checkoutService = checkoutService;
        _mapper = mapper;
        _profiles = profiles;
        _urls = urls;
        _signedTokens = signedTokens;
        _cookie = cookie;
        _linkGenerator = linkGenerator;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Prepares the embedded checkout page for a hand-off opened with <c>ec_version</c>, or returns
    /// <see langword="null"/> when the checkout can't be embedded and the buyer gets the ordinary hand-off: embedded
    /// checkout is off, the checkout isn't a UCP one, the version isn't the checkout's, or the platform lists no
    /// origins. It writes the frame's cookie and lets the platform's origins frame this response.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="checkout">The agent checkout.</param>
    /// <param name="session">The store checkout session.</param>
    /// <param name="handoffToken">The signed token of the hand-off link.</param>
    public async Task<EmbeddedCheckoutViewModel> StartAsync(HttpContext httpContext, AgentCheckout checkout, CheckoutSession session, string handoffToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(checkout);
        ArgumentNullException.ThrowIfNull(session);

        var query = httpContext.Request.Query;
        var version = query[UcpConstants.Embedded.VersionParameter].ToString();

        if (!string.Equals(checkout.Protocol, AgenticCommerceConstants.Protocols.Ucp, StringComparison.Ordinal) ||
            !UcpConstants.Versions.Contains(version, StringComparer.Ordinal) ||
            !string.Equals(version, checkout.ProtocolVersion, StringComparison.Ordinal))
        {
            return null;
        }

        var platform = await _platforms.FindByIdAsync(checkout.PlatformId, httpContext.RequestAborted);
        var origins = await _policy.GetFramingOriginsAsync(platform);

        if (origins.Count == 0)
        {
            return null;
        }

        var context = new EmbeddedCheckoutContext
        {
            CheckoutId = checkout.ItemId,
            SessionId = session.SessionId,
            PlatformId = platform.ItemId,
            Version = version,
        };

        // ec_auth binds the session to the platform's OAuth client. The store can only check it when the platform has
        // an OAuth client and the OpenID validation feature is on; otherwise the signed link alone authorizes the
        // session, as it does for the ordinary hand-off.
        int? authExpiresIn = null;
        var credential = query[UcpConstants.Embedded.AuthParameter].ToString();

        if (!string.IsNullOrEmpty(credential) && CanValidateCredentials(httpContext, platform))
        {
            context.AuthRequired = true;

            if (await ValidateCredentialAsync(httpContext, platform, credential) is { } until)
            {
                context.AuthenticatedUntilUtc = until;
                authExpiresIn = Math.Max(0, (int)(until - _clock.UtcNow).TotalSeconds);
            }
        }

        _cookie.Write(httpContext, context);

        EmbeddingPolicy.Apply(httpContext.Response, origins, partitionCookies: httpContext.Request.IsHttps);
        httpContext.Response.Headers.CacheControl = "no-store";
        httpContext.Response.Headers["Referrer-Policy"] = "same-origin";

        var colorScheme = query[UcpConstants.Embedded.ColorSchemeParameter].ToString();
        var site = await _siteService.GetSiteSettingsAsync();
        var token = _signedTokens.Protect(AgenticCommerceStorage.EmbeddedCheckoutTokenPurpose, checkout.ItemId, _tokenLifetime);
        var baseUrl = await _urls.GetBaseUrlAsync();
        var checkoutUrl = session.Status == CheckoutSessionStatus.Completed
            ? _linkGenerator.GetPathByRouteValues(httpContext, CheckoutConstants.RouteNames.Confirmation, new { sessionId = session.SessionId })
            : _linkGenerator.GetPathByRouteValues(httpContext, CheckoutConstants.RouteNames.Step, new { sessionId = session.SessionId });

        var config = new JsonObject
        {
            ["version"] = version,
            ["hostOrigins"] = new JsonArray([.. origins.Select(origin => (JsonNode)origin)]),
            ["delegate"] = new JsonArray([.. UcpConstants.Embedded.Delegations.Select(name => (JsonNode)name)]),
            ["checkoutUrl"] = checkoutUrl,
            ["checkoutPaths"] = new JsonArray([.. _policy.GetCheckoutPaths(httpContext, session.SessionId).Select(path => (JsonNode)path)]),
            ["stateUrl"] = $"{baseUrl}/{AgentSiteUrls.EmbeddedPath}/{Uri.EscapeDataString(token)}/checkout",
            ["authUrl"] = $"{baseUrl}/{AgentSiteUrls.EmbeddedPath}/{Uri.EscapeDataString(token)}/auth",
            ["continueUrl"] = $"{baseUrl}/{AgentSiteUrls.HandoffPath}/{Uri.EscapeDataString(handoffToken)}",
            ["colorScheme"] = colorScheme is "light" or "dark" ? colorScheme : null,
            ["auth"] = context.AuthRequired
                ? new JsonObject
                {
                    ["type"] = UcpConstants.Embedded.AuthType,
                    ["required"] = context.AuthenticatedUntilUtc is null,
                    ["expiresIn"] = authExpiresIn,
                }
                : null,
        };

        return new EmbeddedCheckoutViewModel
        {
            Title = site?.SiteName,
            CheckoutUrl = checkoutUrl,
            ColorScheme = colorScheme is "light" or "dark" ? colorScheme : null,
            Config = config.ToJsonString(),
        };
    }

    /// <summary>
    /// Returns the agent checkout an embedded checkout's token names, or <see langword="null"/> when the token isn't
    /// valid any more.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<AgentCheckout> FindCheckoutAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token) ||
            _signedTokens.Unprotect(AgenticCommerceStorage.EmbeddedCheckoutTokenPurpose, token) is not { Length: > 0 } checkoutId)
        {
            return null;
        }

        return await _checkouts.FindByIdAsync(checkoutId, cancellationToken);
    }

    /// <summary>
    /// Returns the checkout as UCP JSON, the full state an embedded checkout sends the platform, or
    /// <see langword="null"/> when it is gone or its platform can't embed it any more.
    /// </summary>
    /// <param name="checkout">The agent checkout.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<JsonObject> GetCheckoutJsonAsync(AgentCheckout checkout, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkout);

        var platform = await _platforms.FindByIdAsync(checkout.PlatformId, cancellationToken);

        if ((await _policy.GetFramingOriginsAsync(platform)).Count == 0)
        {
            return null;
        }

        var version = UcpConstants.Versions.Contains(checkout.ProtocolVersion, StringComparer.Ordinal) ? checkout.ProtocolVersion : UcpConstants.Versions[0];
        var offer = await _profiles.GetOfferAsync(version);

        var request = new AgentRequest
        {
            Platform = platform,
            Version = version,
            Capabilities = offer.Capabilities
                .Where(capability => _stateCapabilities.Contains(capability.Name))
                .ToDictionary(capability => capability.Name, capability => capability.Version, StringComparer.Ordinal),
            Context = new AgentCheckoutContext
            {
                PlatformId = platform.ItemId,
                Protocol = AgenticCommerceConstants.Protocols.Ucp,
                ProtocolVersion = version,
                TestMode = platform.TestMode,
                AllowedPaymentHandlers = platform.AllowedPaymentHandlers ?? [],
                UserId = checkout.UserId,
            },
        };

        var result = await _checkoutService.GetAsync(request.Context, checkout.ItemId, cancellationToken);

        if (result.View is null)
        {
            return null;
        }

        return await _mapper.WriteAsync(request, result, cancellationToken);
    }

    /// <summary>
    /// Checks a credential the platform gave and, when it holds, marks the frame's cookie as authorized until it runs
    /// out.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="checkout">The agent checkout.</param>
    /// <param name="credential">The credential.</param>
    /// <returns>Until when the credential holds, or <see langword="null"/> when it doesn't.</returns>
    public async Task<DateTime?> AuthorizeAsync(HttpContext httpContext, AgentCheckout checkout, string credential)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(checkout);

        var context = _cookie.Read(httpContext);

        if (context is null || !string.Equals(context.CheckoutId, checkout.ItemId, StringComparison.Ordinal))
        {
            return null;
        }

        var platform = await _platforms.FindByIdAsync(checkout.PlatformId, httpContext.RequestAborted);

        if ((await _policy.GetFramingOriginsAsync(platform)).Count == 0 ||
            !CanValidateCredentials(httpContext, platform) ||
            await ValidateCredentialAsync(httpContext, platform, credential) is not { } until)
        {
            return null;
        }

        context.AuthRequired = true;
        context.AuthenticatedUntilUtc = until;

        _cookie.Write(httpContext, context);
        EmbeddingPolicy.Apply(httpContext.Response, [], partitionCookies: httpContext.Request.IsHttps);

        return until;
    }

    private static bool CanValidateCredentials(HttpContext httpContext, AgentPlatform platform)
        => !string.IsNullOrEmpty(platform?.OAuthClientId) && httpContext.RequestServices.GetService<OpenIddictValidationService>() is not null;

    // The credential is an access token the store's OpenID server issued to the platform's OAuth client.
    private async Task<DateTime?> ValidateCredentialAsync(HttpContext httpContext, AgentPlatform platform, string credential)
    {
        if (string.IsNullOrWhiteSpace(credential) || httpContext.RequestServices.GetService<OpenIddictValidationService>() is not { } validator)
        {
            return null;
        }

        ClaimsPrincipal principal;

        try
        {
            principal = await validator.ValidateAccessTokenAsync(credential, httpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("An embedded checkout credential for the agent platform '{PlatformId}' was refused: {Reason}", platform.ItemId.SanitizeLogValue(), exception.Message.SanitizeLogValue());
            }

            return null;
        }

        var clientId = principal?.FindFirstValue(OpenIddictConstants.Claims.ClientId)
            ?? principal?.FindFirstValue(OpenIddictConstants.Claims.AuthorizedParty)
            ?? principal?.GetPresenters().FirstOrDefault();

        if (!string.Equals(clientId, platform.OAuthClientId, StringComparison.Ordinal))
        {
            return null;
        }

        var expires = principal.GetExpirationDate();
        var now = _clock.UtcNow;

        if (expires is { } date && date.UtcDateTime <= now)
        {
            return null;
        }

        return expires?.UtcDateTime ?? now.Add(_tokenLifetime);
    }
}
