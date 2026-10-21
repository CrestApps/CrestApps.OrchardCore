using CrestApps.OrchardCore.Core.Services;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The short-lived token on a guest's order confirmation that lets them make an account and take the order into it.
/// Only the confirmation page hands it out, and it names one order.
/// </summary>
public sealed class OrderAccountClaims
{
    private const string ProtectionPurpose = "CrestApps.OrchardCore.Orders.AccountClaim";

    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(2);

    private readonly SignedTokenService _signedTokenService;

    public OrderAccountClaims(SignedTokenService signedTokenService)
    {
        _signedTokenService = signedTokenService;
    }

    public string Create(string orderId)
        => _signedTokenService.Protect(ProtectionPurpose, orderId, _lifetime);

    /// <summary>
    /// Returns the order id the token names, or <see langword="null"/> when it is altered or expired.
    /// </summary>
    public string Read(string token)
        => _signedTokenService.Unprotect(ProtectionPurpose, token);
}
