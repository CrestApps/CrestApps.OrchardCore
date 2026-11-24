using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Learning.Lti.Core.Launch;

/// <summary>
/// The platform as the site describes it: the issuer is the site's base URL when one is set, else the scheme, host and
/// path base of the current request; the instance id is derived from the tenant's name and id, so it never changes.
/// </summary>
public sealed class LtiPlatform : ILtiPlatform
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ShellSettings _shellSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="LtiPlatform"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    /// <param name="shellSettings">The tenant.</param>
    public LtiPlatform(ISiteService siteService, IHttpContextAccessor httpContextAccessor, ShellSettings shellSettings)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _shellSettings = shellSettings;
    }

    /// <inheritdoc />
    public async Task<LtiPlatformInfo> GetAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var request = _httpContextAccessor.HttpContext?.Request;
        var issuer = !string.IsNullOrWhiteSpace(site?.BaseUrl)
            ? site.BaseUrl.TrimEnd('/')
            : request is null || !request.Host.HasValue
                ? "http://localhost"
                : $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("lti-platform:" + _shellSettings.Name + ":" + _shellSettings.TenantId));
        var guid = new Guid(hash.AsSpan(0, 16)).ToString("D");

        return new LtiPlatformInfo
        {
            Issuer = issuer,
            Guid = guid,
            Name = site?.SiteName,
            AuthorizationEndpoint = issuer + "/learning/lti/authorize",
            TokenEndpoint = issuer + "/connect/token",
            JwksUrl = issuer + "/learning/lti/jwks",
            DeepLinkingReturnUrl = issuer + "/learning/lti/deep-linking/return",
        };
    }
}
