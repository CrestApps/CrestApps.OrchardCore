using CrestApps.OrchardCore.Core.Services;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The signed link in a recovery email that brings a buyer back to the order they left: its checkout if it is still
/// open, or a cart with the same items.
/// </summary>
public sealed class CheckoutRecoveryLinks
{
    private const string ProtectionPurpose = "CrestApps.OrchardCore.Orders.Recovery";

    private static readonly TimeSpan _lifetime = TimeSpan.FromDays(14);

    private readonly SignedTokenService _signedTokenService;
    private readonly SiteUrlGenerator _siteUrlGenerator;

    public CheckoutRecoveryLinks(
        SignedTokenService signedTokenService,
        SiteUrlGenerator siteUrlGenerator)
    {
        _signedTokenService = signedTokenService;
        _siteUrlGenerator = siteUrlGenerator;
    }

    /// <summary>
    /// Returns the absolute link for an order, from the site's base URL, or <see langword="null"/> without one.
    /// </summary>
    public Task<string> CreateUrlAsync(string orderId)
        => _siteUrlGenerator.GetSiteUrlAsync("OrderResume", new { token = _signedTokenService.Protect(ProtectionPurpose, orderId, _lifetime) });

    public string Read(string token)
        => _signedTokenService.Unprotect(ProtectionPurpose, token);
}
