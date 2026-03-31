using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AI.Chat.Copilot.Services;

public sealed class CopilotCallbackUrlProvider
{
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;
    private readonly ILogger<CopilotCallbackUrlProvider> _logger;

    public CopilotCallbackUrlProvider(
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        ILogger<CopilotCallbackUrlProvider> logger)
    {
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        _logger = logger;
    }

    public async Task<string> GetCallbackUrlAsync(CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("An active HttpContext is required to build the Copilot OAuth callback URL.");

        var requestCallbackUrl = _linkGenerator.GetUriByAction(httpContext, "OAuthCallback", "CopilotAuth", new
        {
            area = "CrestApps.OrchardCore.AI.Chat.Copilot",
        });

        if (string.IsNullOrWhiteSpace(requestCallbackUrl))
        {
            throw new InvalidOperationException("Unable to build the Copilot OAuth callback URL.");
        }

        var site = await _siteService.GetSiteSettingsAsync();
        if (string.IsNullOrWhiteSpace(site.BaseUrl))
        {
            return requestCallbackUrl;
        }

        if (!Uri.TryCreate(requestCallbackUrl, UriKind.Absolute, out var requestCallbackUri))
        {
            throw new InvalidOperationException("Unable to parse the request-based Copilot OAuth callback URL.");
        }

        if (!Uri.TryCreate(site.BaseUrl, UriKind.Absolute, out var siteBaseUri))
        {
            _logger.LogWarning(
                "Ignoring invalid site base URL '{BaseUrl}' while building the Copilot OAuth callback URL.",
                site.BaseUrl);

            return requestCallbackUrl;
        }

        return BuildSiteAbsoluteUrl(siteBaseUri, requestCallbackUri, httpContext.Request.PathBase).AbsoluteUri;
    }

    public static Uri BuildSiteAbsoluteUrl(Uri siteBaseUri, Uri requestUri, PathString requestPathBase)
    {
        ArgumentNullException.ThrowIfNull(siteBaseUri);
        ArgumentNullException.ThrowIfNull(requestUri);

        var relativePath = requestUri.AbsolutePath;
        var requestPathBaseValue = requestPathBase.Value?.TrimEnd('/');

        if (!string.IsNullOrEmpty(requestPathBaseValue) &&
            relativePath.StartsWith(requestPathBaseValue, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath[requestPathBaseValue.Length..];
        }

        var siteBasePath = siteBaseUri.AbsolutePath.TrimEnd('/');

        if (!string.IsNullOrEmpty(siteBasePath) &&
            siteBasePath != "/" &&
            relativePath.StartsWith(siteBasePath, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath[siteBasePath.Length..];
        }

        var callbackUri = new Uri(EnsureTrailingSlash(siteBaseUri), relativePath.TrimStart('/'));
        var builder = new UriBuilder(callbackUri)
        {
            Query = requestUri.Query.TrimStart('?'),
        };

        return builder.Uri;
    }

    private static Uri EnsureTrailingSlash(Uri uri)
    {
        if (uri.AbsoluteUri.EndsWith('/'))
        {
            return uri;
        }

        return new Uri(uri.AbsoluteUri + "/", UriKind.Absolute);
    }
}
