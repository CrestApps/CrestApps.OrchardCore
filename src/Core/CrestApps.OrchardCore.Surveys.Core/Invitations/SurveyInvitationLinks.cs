using Microsoft.AspNetCore.Http;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys.Core.Invitations;

/// <summary>
/// Makes the absolute URL of a personal invitation link: from the site's base URL first, since invitations are often
/// sent by a background task, otherwise from the current request.
/// </summary>
public sealed class SurveyInvitationLinks
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyInvitationLinks"/> class.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SurveyInvitationLinks(
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <summary>
    /// Builds the link of a token.
    /// </summary>
    /// <param name="token">The invitation token.</param>
    /// <returns>The absolute URL, or a site-relative one when neither a base URL nor a request with a host is known.</returns>
    public async Task<string> BuildAsync(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var path = "/s/i/" + Uri.EscapeDataString(token);

        // Read once a scope: a send of many invitations builds a link for each.
        _baseUrl ??= (await _siteService.GetSiteSettingsAsync())?.BaseUrl ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(_baseUrl))
        {
            return _baseUrl.TrimEnd('/') + path;
        }

        var request = _httpContextAccessor.HttpContext?.Request;

        return request is null || !request.Host.HasValue || string.IsNullOrEmpty(request.Scheme)
            ? path
            : $"{request.Scheme}://{request.Host}{request.PathBase}{path}";
    }
}
