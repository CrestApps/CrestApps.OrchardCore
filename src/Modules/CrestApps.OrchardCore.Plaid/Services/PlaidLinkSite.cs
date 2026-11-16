using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.Plaid.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Plaid.Services;

/// <summary>
/// Builds link token requests from the current request: the person's id hashed with the tenant's name (so Plaid never
/// sees a user id, name or e-mail), the site's name, the request's language when Plaid Link speaks it, and this
/// tenant's webhook URL.
/// </summary>
internal sealed class PlaidLinkSite : IPlaidLinkSite
{
    // The languages Plaid Link speaks.
    private static readonly HashSet<string> _languages = new(StringComparer.OrdinalIgnoreCase)
    {
        "da", "de", "en", "es", "et", "fr", "it", "lt", "lv", "nl", "no", "pl", "pt", "ro", "sv",
    };

    private readonly ISiteService _siteService;
    private readonly ShellSettings _shellSettings;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public PlaidLinkSite(ISiteService siteService, ShellSettings shellSettings, IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator)
    {
        _siteService = siteService;
        _shellSettings = shellSettings;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    public async Task<PlaidLinkTokenRequest> CreateLinkTokenRequestAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(userId);

        var site = await _siteService.GetSiteSettingsAsync();
        var language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var httpContext = _httpContextAccessor.HttpContext;

        return new PlaidLinkTokenRequest
        {
            ClientUserId = ClientUserId(_shellSettings.Name, userId),
            ClientName = site.SiteName,
            Language = _languages.Contains(language) ? language : "en",
            WebhookUrl = httpContext is null ? null : _linkGenerator.GetUriByName(httpContext, PlaidConstants.WebhookRouteName, values: null),
        };
    }

    /// <summary>
    /// Returns the opaque id Plaid knows a person by.
    /// </summary>
    internal static string ClientUserId(string tenantName, string userId)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tenantName + ":" + userId)));
}
