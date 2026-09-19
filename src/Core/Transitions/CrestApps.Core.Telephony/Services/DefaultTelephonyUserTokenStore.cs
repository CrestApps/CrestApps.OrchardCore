using CrestApps.Core.Security;
using Microsoft.AspNetCore.DataProtection;
using CrestApps.Core.Telephony;
using CrestApps.Core.Telephony.Models;

namespace CrestApps.Core.Telephony.Services;

/// <summary>
/// Stores the current user's telephony tokens on the user's account, encrypting the token values at
/// rest with the data protection provider. Tokens are read with <c>user.TryGet</c> and written with
/// <c>user.Put</c>.
/// </summary>
public sealed class DefaultTelephonyUserTokenStore : ITelephonyUserTokenStore
{
    private readonly IUserProfileStore _userProfileStore;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyUserTokenStore"/> class.
    /// </summary>
    /// <param name="userProfileStore">The user accessor.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public DefaultTelephonyUserTokenStore(
        IUserProfileStore userProfileStore,
        IDataProtectionProvider dataProtectionProvider)
    {
        _userProfileStore = userProfileStore;
        _protector = dataProtectionProvider.CreateProtector(TelephonyConstants.TokenProtectorPurpose);
    }

    /// <inheritdoc/>
    public async Task<TelephonyUserTokens> GetAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return null;
        }

        var connections = await _userProfileStore.FindAsync<TelephonyUserConnections>(cancellationToken);

        if (connections?.Connections is null || !connections.Connections.TryGetValue(providerName, out var stored) || stored is null)
        {
            return null;
        }

        return Unprotect(stored);
    }

    /// <inheritdoc/>
    public async Task StoreAsync(string providerName, TelephonyUserTokens tokens, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        ArgumentNullException.ThrowIfNull(tokens);

        await _userProfileStore.UpdateAsync<TelephonyUserConnections>(
            connections =>
            {
                connections.Connections ??= [];
                connections.Connections[providerName] = Protect(providerName, tokens);

                return true;
            },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return;
        }

        await _userProfileStore.UpdateAsync<TelephonyUserConnections>(
            connections => connections.Connections is not null && connections.Connections.Remove(providerName),
            cancellationToken);
    }

    private TelephonyUserTokens Protect(string providerName, TelephonyUserTokens tokens)
    {
        return new TelephonyUserTokens
        {
            ProviderName = providerName,
            AccessToken = string.IsNullOrEmpty(tokens.AccessToken) ? null : _protector.Protect(tokens.AccessToken),
            RefreshToken = string.IsNullOrEmpty(tokens.RefreshToken) ? null : _protector.Protect(tokens.RefreshToken),
            ExpiresUtc = tokens.ExpiresUtc,
            TokenType = tokens.TokenType,
            Scope = tokens.Scope,
            RemoteUserId = tokens.RemoteUserId,
            RemoteUserName = tokens.RemoteUserName,
            RemoteUserEmail = tokens.RemoteUserEmail,
            RemotePhoneNumber = tokens.RemotePhoneNumber,
        };
    }

    private TelephonyUserTokens Unprotect(TelephonyUserTokens stored)
    {
        return new TelephonyUserTokens
        {
            ProviderName = stored.ProviderName,
            AccessToken = string.IsNullOrEmpty(stored.AccessToken) ? null : _protector.Unprotect(stored.AccessToken),
            RefreshToken = string.IsNullOrEmpty(stored.RefreshToken) ? null : _protector.Unprotect(stored.RefreshToken),
            ExpiresUtc = stored.ExpiresUtc,
            TokenType = stored.TokenType,
            Scope = stored.Scope,
            RemoteUserId = stored.RemoteUserId,
            RemoteUserName = stored.RemoteUserName,
            RemoteUserEmail = stored.RemoteUserEmail,
            RemotePhoneNumber = stored.RemotePhoneNumber,
        };
    }
}
