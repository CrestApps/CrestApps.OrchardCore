using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Routing;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The signed link in a recovery email that brings a buyer back to the order they left: its checkout if it is still
/// open, or a cart with the same items.
/// </summary>
public sealed class CheckoutRecoveryLinks
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(14);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly LinkGenerator _linkGenerator;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;

    public CheckoutRecoveryLinks(IDataProtectionProvider dataProtectionProvider, LinkGenerator linkGenerator, ISiteService siteService, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector("CrestApps.OrchardCore.Orders.Recovery").ToTimeLimitedDataProtector();
        _linkGenerator = linkGenerator;
        _siteService = siteService;
        _clock = clock;
    }

    /// <summary>
    /// Returns the absolute link for an order, from the site's base URL, or <see langword="null"/> without one.
    /// </summary>
    public async Task<string> CreateUrlAsync(string orderId)
    {
        var site = await _siteService.GetSiteSettingsAsync();

        if (string.IsNullOrEmpty(site.BaseUrl))
        {
            return null;
        }

        var path = _linkGenerator.GetPathByRouteValues("OrderResume", new { token = _protector.Protect(orderId, _clock.UtcNow + _lifetime) }, pathBase: string.Empty);

        return string.IsNullOrEmpty(path) ? null : site.BaseUrl.TrimEnd('/') + path;
    }

    public string Read(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(token, out var expires) is { Length: > 0 } orderId && expires > _clock.UtcNow ? orderId : null;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return null;
        }
    }
}
