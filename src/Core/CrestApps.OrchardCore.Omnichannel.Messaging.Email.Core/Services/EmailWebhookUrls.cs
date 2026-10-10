using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Webhooks;
using Microsoft.AspNetCore.Http;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Services;

/// <summary>
/// The default <see cref="IEmailWebhookUrls"/>. The URLs are built on the site's public base URL when one is set, so
/// they are the ones the provider can reach, and on the current request otherwise.
/// </summary>
public sealed class EmailWebhookUrls : IEmailWebhookUrls
{
    private readonly ISiteService _siteService;
    private readonly IEmailSecretProtector _secretProtector;
    private readonly IEnumerable<IInboundEmailWebhookParser> _parsers;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailWebhookUrls"/> class.
    /// </summary>
    /// <param name="siteService">The site service the settings and base URL are read from.</param>
    /// <param name="secretProtector">The protector the webhook key is read with.</param>
    /// <param name="parsers">The registered provider formats.</param>
    /// <param name="httpContextAccessor">The accessor of the current request.</param>
    public EmailWebhookUrls(
        ISiteService siteService,
        IEmailSecretProtector secretProtector,
        IEnumerable<IInboundEmailWebhookParser> parsers,
        IHttpContextAccessor httpContextAccessor)
    {
        _siteService = siteService;
        _secretProtector = secretProtector;
        _parsers = parsers;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<KeyValuePair<string, string>>> GetAllAsync()
    {
        var settings = await _siteService.GetSettingsAsync<EmailInboundSettings>();
        var key = _secretProtector.Unprotect(settings.WebhookKey);

        if (string.IsNullOrEmpty(key))
        {
            return [];
        }

        var baseUrl = await GetBaseUrlAsync();

        if (string.IsNullOrEmpty(baseUrl))
        {
            return [];
        }

        return _parsers
            .Select(parser => KeyValuePair.Create(
                parser.Name,
                $"{baseUrl}/{EmailChannelConstants.InboundWebhookRoute.Replace("{provider}", parser.Name, StringComparison.Ordinal)}?key={Uri.EscapeDataString(key)}"))
            .ToArray();
    }

    private async Task<string> GetBaseUrlAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();

        if (!string.IsNullOrWhiteSpace(site.BaseUrl) && Uri.TryCreate(site.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return baseUri.AbsoluteUri.TrimEnd('/');
        }

        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null
            ? null
            : $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
    }
}
