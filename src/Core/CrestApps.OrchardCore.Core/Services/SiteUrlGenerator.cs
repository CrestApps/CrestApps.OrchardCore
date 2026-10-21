using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Creates the absolute URL of a named route for a link that leaves the site, such as a link in an email.
/// </summary>
/// <remarks>
/// The site's configured base URL comes first: an email is often sent by a background task, where there is no
/// request, or from an administrator's request on an internal address, and the link has to work for the customer. The
/// base URL already holds the tenant's prefix, so the route's path goes after it as is.
/// </remarks>
public sealed class SiteUrlGenerator
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SiteUrlGenerator"/> class.
    /// </summary>
    /// <param name="siteService">The site service, for the site's base URL.</param>
    /// <param name="httpContextAccessor">The accessor for the current request, used when no base URL is configured.</param>
    /// <param name="linkGenerator">The link generator.</param>
    public SiteUrlGenerator(
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    /// <summary>
    /// Returns the route's absolute URL from the site's base URL, or from the current request when no base URL is
    /// configured. Returns <see langword="null"/> when neither can build it.
    /// </summary>
    /// <param name="routeName">The name of the route.</param>
    /// <param name="values">The route values.</param>
    public async Task<string> GetUrlAsync(string routeName, object values)
    {
        ArgumentException.ThrowIfNullOrEmpty(routeName);

        var url = await GetSiteUrlAsync(routeName, values);

        if (url is not null)
        {
            return url;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        return httpContext is null
            ? null
            : _linkGenerator.GetUriByRouteValues(httpContext, routeName, values);
    }

    /// <summary>
    /// Returns the route's absolute URL from the site's base URL only, or <see langword="null"/> when no base URL is
    /// configured or the route can't be built.
    /// </summary>
    /// <param name="routeName">The name of the route.</param>
    /// <param name="values">The route values.</param>
    public async Task<string> GetSiteUrlAsync(string routeName, object values)
    {
        ArgumentException.ThrowIfNullOrEmpty(routeName);

        var site = await _siteService.GetSiteSettingsAsync();

        if (string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return null;
        }

        var path = _linkGenerator.GetPathByRouteValues(routeName, values, pathBase: PathString.Empty);

        return string.IsNullOrEmpty(path)
            ? null
            : site.BaseUrl.TrimEnd('/') + path;
    }
}
