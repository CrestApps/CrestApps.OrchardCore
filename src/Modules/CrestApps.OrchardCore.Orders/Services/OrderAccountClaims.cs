using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// The short-lived token on a guest's order confirmation that lets them make an account and take the order into it.
/// Only the confirmation page hands it out, and it names one order.
/// </summary>
public sealed class OrderAccountClaims
{
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(2);

    private readonly ITimeLimitedDataProtector _protector;
    private readonly IClock _clock;

    public OrderAccountClaims(IDataProtectionProvider dataProtectionProvider, IClock clock)
    {
        _protector = dataProtectionProvider.CreateProtector("CrestApps.OrchardCore.Orders.AccountClaim").ToTimeLimitedDataProtector();
        _clock = clock;
    }

    public string Create(string orderId)
        => _protector.Protect(orderId, _clock.UtcNow + _lifetime);

    /// <summary>
    /// Returns the order id the token names, or <see langword="null"/> when it is altered or expired.
    /// </summary>
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
