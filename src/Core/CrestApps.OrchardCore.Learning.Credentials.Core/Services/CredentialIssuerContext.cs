using Microsoft.AspNetCore.Http;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Credentials.Core.Services;

/// <summary>
/// Tells who the issuer is and where its addresses live: the site's base URL when it is set, else the address of the
/// current request.
/// </summary>
public class CredentialIssuerContext
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialIssuerContext"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public CredentialIssuerContext(ISiteService siteService, IHttpContextAccessor httpContextAccessor)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Gets the issuer.
    /// </summary>
    /// <returns>The issuer, or <see langword="null"/> when the site's address is unknown.</returns>
    public virtual async Task<CredentialIssuer> GetAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var baseUrl = site.BaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl) && _httpContextAccessor.HttpContext?.Request is { Host.HasValue: true } request)
        {
            baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        }

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return null;
        }

        return new CredentialIssuer(baseUrl.Trim().TrimEnd('/'), string.IsNullOrWhiteSpace(site.SiteName) ? baseUrl : site.SiteName);
    }
}
