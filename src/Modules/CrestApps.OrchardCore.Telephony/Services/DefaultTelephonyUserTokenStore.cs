using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Entities;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Stores the current user's telephony tokens on the user's account, encrypting the token values at
/// rest with the data protection provider. Tokens are read with <c>user.TryGet</c> and written with
/// <c>user.Put</c>.
/// </summary>
public sealed class DefaultTelephonyUserTokenStore : ITelephonyUserTokenStore
{
    private readonly ITelephonyUserAccessor _userAccessor;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyUserTokenStore"/> class.
    /// </summary>
    /// <param name="userAccessor">The user accessor.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public DefaultTelephonyUserTokenStore(
        ITelephonyUserAccessor userAccessor,
        IDataProtectionProvider dataProtectionProvider)
    {
        _userAccessor = userAccessor;
        _protector = dataProtectionProvider.CreateProtector(TelephonyConstants.TokenProtectorPurpose);
    }

    /// <inheritdoc/>
    public async Task<TelephonyUserTokens> GetAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return null;
        }

        var user = await _userAccessor.GetCurrentUserAsync();

        if (user is not IEntity entity || !entity.TryGet<TelephonyUserConnections>(out var connections))
        {
            return null;
        }

        if (connections.Connections is null || !connections.Connections.TryGetValue(providerName, out var stored) || stored is null)
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

        var user = await _userAccessor.GetCurrentUserAsync();

        if (user is not IEntity entity)
        {
            return;
        }

        var connections = entity.GetOrCreate<TelephonyUserConnections>();
        connections.Connections ??= [];
        connections.Connections[providerName] = Protect(providerName, tokens);

        entity.Put(connections);

        await _userAccessor.UpdateUserAsync(user);
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return;
        }

        var user = await _userAccessor.GetCurrentUserAsync();

        if (user is not IEntity entity || !entity.TryGet<TelephonyUserConnections>(out var connections) || connections.Connections is null)
        {
            return;
        }

        if (connections.Connections.Remove(providerName))
        {
            entity.Put(connections);

            await _userAccessor.UpdateUserAsync(user);
        }
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
        };
    }
}
