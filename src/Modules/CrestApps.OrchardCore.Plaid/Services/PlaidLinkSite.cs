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

        return new PlaidLinkTokenRequest
        {
            ClientUserId = ClientUserId(_shellSettings.Name, userId),
            ClientName = site.SiteName,
            Language = _languages.Contains(language) ? language : "en",
            WebhookUrl = await GetWebhookUrlAsync(cancellationToken),
        };
    }

    public async Task<string> GetWebhookUrlAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is not null)
        {
            return _linkGenerator.GetUriByName(httpContext, PlaidConstants.WebhookRouteName, values: null);
        }

        var site = await _siteService.GetSiteSettingsAsync();

        return WebhookUrlFrom(site.BaseUrl);
    }

    /// <summary>
    /// Returns the webhook URL under a site's base URL, which holds the tenant's prefix, or <see langword="null"/> when
    /// the base URL is not an absolute address.
    /// </summary>
    internal static string WebhookUrlFrom(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Path).TrimEnd('/') + "/" + PlaidConstants.WebhookPath;
    }

    /// <summary>
    /// Returns the opaque id Plaid knows a person by.
    /// </summary>
    internal static string ClientUserId(string tenantName, string userId)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(tenantName + ":" + userId)));
}
