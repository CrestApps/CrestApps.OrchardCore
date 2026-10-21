using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Core.Services;

/// <summary>
/// Creates and reads signed, time-limited tokens, such as the token in a link that opens one order or pays one
/// transaction.
/// </summary>
/// <remarks>
/// A token is the value protected with the tenant's data protection keys, for one purpose, with an expiry. It cannot be
/// forged, altered to name another value, used for another purpose, or used after it expires.
/// </remarks>
public sealed class SignedTokenService
{
    private readonly IDataProtectionProvider _dataProtectionProvider;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignedTokenService"/> class.
    /// </summary>
    /// <param name="dataProtectionProvider">The tenant's data protection provider.</param>
    /// <param name="clock">The clock that sets and checks the expiry.</param>
    public SignedTokenService(
        IDataProtectionProvider dataProtectionProvider,
        IClock clock)
    {
        _dataProtectionProvider = dataProtectionProvider;
        _clock = clock;
    }

    /// <summary>
    /// Returns a token that holds the value for the given purpose until the lifetime ends.
    /// </summary>
    /// <param name="purpose">The purpose the token is for. Only the same purpose reads it.</param>
    /// <param name="value">The value the token holds.</param>
    /// <param name="lifetime">How long the token keeps working.</param>
    public string Protect(string purpose, string value, TimeSpan lifetime)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);
        ArgumentNullException.ThrowIfNull(value);

        return CreateProtector(purpose).Protect(value, _clock.UtcNow + lifetime);
    }

    /// <summary>
    /// Returns the value a token holds, or <see langword="null"/> when the token is empty, altered, expired, created for
    /// another purpose or protected with another site's keys.
    /// </summary>
    /// <param name="purpose">The purpose the token was created for.</param>
    /// <param name="token">The token to read.</param>
    public string Unprotect(string purpose, string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(purpose);

        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            return CreateProtector(purpose).Unprotect(token, out var expires) is { Length: > 0 } value && expires > _clock.UtcNow
                ? value
                : null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private ITimeLimitedDataProtector CreateProtector(string purpose)
        => _dataProtectionProvider.CreateProtector(purpose).ToTimeLimitedDataProtector();
}
