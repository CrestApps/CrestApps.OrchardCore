using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Ucp;

/// <summary>
/// Decides who may frame UCP embedded checkout and where: the platform that owns a checkout, from the origins it lists,
/// only when embedded checkout is allowed, and only on the store checkout pages of that checkout's session.
/// </summary>
public sealed class EmbeddedCheckoutPolicy
{
    private readonly ISiteService _siteService;
    private readonly IAgentPlatformStore _platforms;
    private readonly LinkGenerator _linkGenerator;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmbeddedCheckoutPolicy"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="platforms">The agent platforms.</param>
    /// <param name="linkGenerator">The link generator.</param>
    /// <param name="clock">The clock.</param>
    public EmbeddedCheckoutPolicy(
        ISiteService siteService,
        IAgentPlatformStore platforms,
        LinkGenerator linkGenerator,
        IClock clock)
    {
        _siteService = siteService;
        _platforms = platforms;
        _linkGenerator = linkGenerator;
        _clock = clock;
    }

    /// <summary>
    /// Returns the origins that may frame a platform's checkouts: none unless embedded checkout is allowed, the platform
    /// is enabled and uses UCP, and it lists valid origins.
    /// </summary>
    /// <param name="platform">The platform.</param>
    public async Task<IReadOnlyList<string>> GetFramingOriginsAsync(AgentPlatform platform)
    {
        if (platform is null || !platform.Enabled || !platform.Uses(AgenticCommerceConstants.Protocols.Ucp) ||
            !(await _siteService.GetSettingsAsync<AgenticCommerceSettings>()).AllowEmbeddedCheckout)
        {
            return [];
        }

        return EmbeddingPolicy.NormalizeOrigins(platform.EmbeddedCheckoutOrigins);
    }

    /// <summary>
    /// Returns the origins that may frame the checkout pages of an embedded checkout's frame now: none when the host's
    /// credential ran out.
    /// </summary>
    /// <param name="context">The frame's context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<IReadOnlyList<string>> GetFramingOriginsAsync(EmbeddedCheckoutContext context, CancellationToken cancellationToken = default)
    {
        if (context is null || !context.IsAuthorized(_clock.UtcNow) || string.IsNullOrEmpty(context.PlatformId))
        {
            return [];
        }

        return await GetFramingOriginsAsync(await _platforms.FindByIdAsync(context.PlatformId, cancellationToken));
    }

    /// <summary>
    /// Returns whether embedded checkout is offered to any platform: it is allowed and an enabled UCP platform lists the
    /// origins that frame it.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<bool> IsOfferedAsync(CancellationToken cancellationToken = default)
    {
        if (!(await _siteService.GetSettingsAsync<AgenticCommerceSettings>()).AllowEmbeddedCheckout)
        {
            return false;
        }

        foreach (var platform in await _platforms.GetAllAsync(cancellationToken))
        {
            if ((await GetFramingOriginsAsync(platform)).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns whether a request is for a store checkout page of a session: a step, the payment calls or the
    /// confirmation.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="sessionId">The store checkout session.</param>
    public bool IsCheckoutPath(HttpContext httpContext, string sessionId)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        if (string.IsNullOrEmpty(sessionId))
        {
            return false;
        }

        var path = httpContext.Request.PathBase.Add(httpContext.Request.Path).Value;

        return GetCheckoutPaths(httpContext, sessionId).Any(prefix => EmbeddingPolicy.IsWithin(path, prefix));
    }

    /// <summary>
    /// Returns the paths, with the tenant's prefix, of a session's store checkout pages.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    /// <param name="sessionId">The store checkout session.</param>
    public IReadOnlyList<string> GetCheckoutPaths(HttpContext httpContext, string sessionId)
    {
        var paths = new List<string>(2);

        foreach (var route in new[] { CheckoutConstants.RouteNames.Step, CheckoutConstants.RouteNames.Confirmation })
        {
            if (_linkGenerator.GetPathByRouteValues(httpContext, route, new { sessionId }) is { Length: > 0 } path)
            {
                paths.Add(path);
            }
        }

        return paths;
    }
}
