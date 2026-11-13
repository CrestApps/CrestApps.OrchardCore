using CrestApps.OrchardCore.AdminTheme.Models;
using Microsoft.AspNetCore.Http;
using OrchardCore.Environment.Shell;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.AdminTheme.Services;

/// <summary>
/// Resolves the color scheme of the admin for the current user: the one they picked in the top bar, kept in a cookie,
/// or else the site's default scheme (<see cref="AdminThemeSettings.DefaultColorScheme"/>), or else the built-in one.
/// </summary>
public sealed class AdminColorSchemeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminColorSchemeService"/> class.
    /// </summary>
    public AdminColorSchemeService(IHttpContextAccessor httpContextAccessor, ISiteService siteService, ShellSettings shellSettings)
    {
        _httpContextAccessor = httpContextAccessor;
        _siteService = siteService;

        // Named per tenant, like TheAdmin's color mode cookie ('{tenant}-admintheme').
        CookieName = $"{shellSettings.Name}-adminscheme";
    }

    /// <summary>
    /// The name of the cookie holding the scheme the user picked.
    /// </summary>
    public string CookieName { get; }

    /// <summary>
    /// The scheme the user picked, or <see langword="null"/> when they follow the site's default.
    /// </summary>
    public string GetUserScheme()
    {
        var request = _httpContextAccessor.HttpContext?.Request;

        return request is not null && request.Cookies.TryGetValue(CookieName, out var value)
            ? AdminColorSchemes.Find(value)
            : null;
    }

    /// <summary>
    /// The site's default scheme.
    /// </summary>
    public async Task<string> GetSiteSchemeAsync()
    {
        var settings = await _siteService.GetSettingsAsync<AdminThemeSettings>();

        return AdminColorSchemes.Find(settings.DefaultColorScheme) ?? AdminColorSchemes.BuiltIn;
    }

    /// <summary>
    /// The scheme the page is rendered in.
    /// </summary>
    public async Task<string> GetCurrentSchemeAsync()
        => GetUserScheme() ?? await GetSiteSchemeAsync();

    /// <summary>
    /// The value of the page's 'data-ca-scheme' attribute: <see langword="null"/> for the built-in scheme, so that Razor
    /// leaves the attribute out and the stylesheet's own colors apply.
    /// </summary>
    public async Task<string> GetSchemeAttributeAsync()
    {
        var scheme = await GetCurrentSchemeAsync();

        return scheme == AdminColorSchemes.BuiltIn ? null : scheme;
    }
}
