using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.Http;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AgenticCommerce.Core.Services;

/// <summary>
/// Builds the absolute addresses agents are given: the tenant's base address, and the link that hands an agent checkout
/// to the buyer's browser.
/// </summary>
public sealed class AgentSiteUrls
{
    /// <summary>
    /// The path the hand-off link opens, after the tenant's base address.
    /// </summary>
    public const string HandoffPath = "agentic-commerce/continue";

    /// <summary>
    /// The path of an embedded checkout's own calls (its checkout state and its host's credential), after the tenant's
    /// base address.
    /// </summary>
    public const string EmbeddedPath = "agentic-commerce/embedded";

    private static readonly TimeSpan _handoffLifetime = TimeSpan.FromDays(2);

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;
    private readonly SignedTokenService _signedTokenService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentSiteUrls"/> class.
    /// </summary>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="signedTokenService">The signed token service.</param>
    public AgentSiteUrls(
        IHttpContextAccessor httpContextAccessor,
        ISiteService siteService,
        SignedTokenService signedTokenService)
    {
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;
        _signedTokenService = signedTokenService;
    }

    /// <summary>
    /// Returns the tenant's absolute base address, with no trailing slash: the request's own address (with the tenant's
    /// URL prefix), or the site's base URL outside a request.
    /// </summary>
    public async Task<string> GetBaseUrlAsync()
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        if (request is not null && request.Host.HasValue)
        {
            return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
        }

        var site = await _siteService.GetSiteSettingsAsync();

        return site?.BaseUrl?.TrimEnd('/');
    }

    /// <summary>
    /// Returns an absolute address for a link: an absolute one as it is, a path on the site after the base address.
    /// </summary>
    /// <param name="link">The link.</param>
    public async Task<string> ToAbsoluteAsync(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
        {
            return null;
        }

        link = link.Trim();

        if (Uri.TryCreate(link, UriKind.Absolute, out var absolute) && (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
        {
            return link;
        }

        var baseUrl = await GetBaseUrlAsync();

        return string.IsNullOrEmpty(baseUrl) ? null : baseUrl + "/" + link.TrimStart('/');
    }

    /// <summary>
    /// Returns the link that opens an agent checkout in the store's own checkout, in the buyer's browser.
    /// </summary>
    /// <param name="checkout">The agent checkout.</param>
    public async Task<string> GetHandoffUrlAsync(AgentCheckout checkout)
    {
        ArgumentNullException.ThrowIfNull(checkout);

        var token = _signedTokenService.Protect(AgenticCommerceStorage.HandoffTokenPurpose, checkout.ItemId, _handoffLifetime);

        return $"{await GetBaseUrlAsync()}/{HandoffPath}/{Uri.EscapeDataString(token)}";
    }

    /// <summary>
    /// Returns the agent checkout a hand-off token names, or <see langword="null"/> when it is not valid any more.
    /// </summary>
    /// <param name="token">The token.</param>
    public string ReadHandoffToken(string token)
        => string.IsNullOrEmpty(token) ? null : _signedTokenService.Unprotect(AgenticCommerceStorage.HandoffTokenPurpose, token);
}
