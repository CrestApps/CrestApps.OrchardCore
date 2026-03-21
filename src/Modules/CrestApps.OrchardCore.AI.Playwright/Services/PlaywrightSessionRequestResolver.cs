using System.Security.Claims;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Playwright.Models;
using CrestApps.OrchardCore.AI.Playwright.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.AI.Playwright.Services;

public sealed class PlaywrightSessionRequestResolver : IPlaywrightSessionRequestResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AdminOptions _adminOptions;
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly ILogger<PlaywrightSessionRequestResolver> _logger;

    public PlaywrightSessionRequestResolver(
        IHttpContextAccessor httpContextAccessor,
        IOptions<AdminOptions> adminOptions,
        IDataProtectionProvider dataProtectionProvider,
        ILogger<PlaywrightSessionRequestResolver> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _adminOptions = adminOptions.Value;
        _dataProtectionProvider = dataProtectionProvider;
        _logger = logger;
    }

    public PlaywrightSessionRequest Resolve(object resource, string chatSessionId)
    {
        if (string.IsNullOrWhiteSpace(chatSessionId) || resource is not Entity entity)
        {
            return null;
        }

        var metadata = entity.As<PlaywrightSessionMetadata>() ?? new PlaywrightSessionMetadata();

        if (resource is AIProfile profile)
        {
            var legacySettings = profile.GetSettings<PlaywrightProfileSettings>();
            metadata.Enabled = metadata.Enabled || legacySettings.Enabled;
        }

        if (!metadata.Enabled)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext is null)
        {
            _logger.LogWarning("Playwright session resolution failed because no HttpContext is available.");
            return null;
        }

        var baseUrl = NormalizeUrl(
            metadata.BaseUrl,
            $"{httpContext.Request.Scheme}://{httpContext.Request.Host}{httpContext.Request.PathBase}");

        var adminBaseUrl = NormalizeUrl(
            metadata.AdminBaseUrl,
            CombineUrl(baseUrl, _adminOptions.AdminUrlPrefix));
        var username = metadata.Username?.Trim();
        var password = UnprotectPassword(metadata.ProtectedPassword);
        var inactivityTimeoutInMinutes = resource is AIProfile profileResource
            ? Math.Max(1, profileResource.GetSettings<AIProfileDataExtractionSettings>()?.SessionInactivityTimeoutInMinutes ?? PlaywrightConstants.DefaultSessionInactivityTimeoutInMinutes)
            : PlaywrightConstants.DefaultSessionInactivityTimeoutInMinutes;

        return new PlaywrightSessionRequest
        {
            ChatSessionId = chatSessionId,
            OwnerId = ResolveOwnerId(resource, httpContext.User),
            ResourceItemId = GetResourceItemId(resource),
            BaseUrl = baseUrl,
            AdminBaseUrl = adminBaseUrl,
            BrowserMode = PlaywrightBrowserMode.PersistentContext,
            Username = username,
            Password = password,
            CanAttemptLogin = !string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password),
            PersistentProfilePath = metadata.PersistentProfilePath?.Trim(),
            Headless = metadata.Headless,
            PublishByDefault = metadata.PublishByDefault,
            SessionInactivityTimeoutInMinutes = inactivityTimeoutInMinutes,
        };
    }

    private static string ResolveOwnerId(object resource, ClaimsPrincipal user)
    {
        return resource switch
        {
            AIProfile profile when !string.IsNullOrWhiteSpace(profile.OwnerId) => profile.OwnerId,
            ChatInteraction interaction when !string.IsNullOrWhiteSpace(interaction.OwnerId) => interaction.OwnerId,
            _ => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.Identity?.Name ?? string.Empty,
        };
    }

    private static string GetResourceItemId(object resource)
    {
        return resource switch
        {
            CrestApps.OrchardCore.Models.CatalogItem item => item.ItemId,
            _ => null,
        };
    }

    private static string NormalizeUrl(string configuredUrl, string fallbackUrl)
    {
        var value = string.IsNullOrWhiteSpace(configuredUrl) ? fallbackUrl : configuredUrl;
        return value.Trim().TrimEnd('/');
    }

    private string UnprotectPassword(string protectedPassword)
    {
        if (string.IsNullOrWhiteSpace(protectedPassword))
        {
            return null;
        }

        try
        {
            var protector = _dataProtectionProvider.CreateProtector(PlaywrightConstants.ProtectorName);
            return protector.Unprotect(protectedPassword);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to unprotect the saved Playwright password.");
            return null;
        }
    }

    internal static string CombineUrl(string baseUrl, string relativeSegment)
    {
        var trimmedBase = baseUrl?.TrimEnd('/') ?? string.Empty;
        var trimmedSegment = relativeSegment?.Trim().Trim('/');

        if (string.IsNullOrWhiteSpace(trimmedSegment))
        {
            return trimmedBase;
        }

        return $"{trimmedBase}/{trimmedSegment}";
    }
}
