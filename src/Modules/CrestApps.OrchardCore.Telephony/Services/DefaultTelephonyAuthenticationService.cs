using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Telephony.Services;

/// <summary>
/// Default <see cref="ITelephonyAuthenticationService"/> implementation that coordinates the OAuth
/// flow with the configured default provider and persists tokens through the token store.
/// </summary>
public sealed class DefaultTelephonyAuthenticationService : ITelephonyAuthenticationService
{
    private readonly ISiteService _siteService;
    private readonly ITelephonyProviderResolver _providerResolver;
    private readonly ITelephonyUserTokenStore _tokenStore;
    private readonly ITelephonyUserAccessor _userAccessor;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly TimeSpan _tokenRefreshLockTimeout;
    private readonly TimeSpan _tokenRefreshLockExpiration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyAuthenticationService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the default provider name.</param>
    /// <param name="providerResolver">The telephony provider resolver.</param>
    /// <param name="tokenStore">The user token store.</param>
    /// <param name="userAccessor">The accessor used to identify the current user when serializing token refreshes.</param>
    /// <param name="distributedLock">The distributed lock used to serialize concurrent token refreshes per user and provider.</param>
    /// <param name="clock">The clock used to evaluate token expiration.</param>
    /// <param name="coordinationOptions">The distributed-lock timings this deployment coordinates with.</param>
    /// <param name="logger">The logger used to record incomplete remote revocations.</param>
    public DefaultTelephonyAuthenticationService(
        ISiteService siteService,
        ITelephonyProviderResolver providerResolver,
        ITelephonyUserTokenStore tokenStore,
        ITelephonyUserAccessor userAccessor,
        IDistributedLock distributedLock,
        IClock clock,
        IOptions<TelephonyCoordinationOptions> coordinationOptions,
        ILogger<DefaultTelephonyAuthenticationService> logger)
    {
        _siteService = siteService;
        _providerResolver = providerResolver;
        _tokenStore = tokenStore;
        _userAccessor = userAccessor;
        _distributedLock = distributedLock;
        _clock = clock;
        _logger = logger;
        _tokenRefreshLockTimeout = coordinationOptions.Value.TokenRefreshLockTimeout;
        _tokenRefreshLockExpiration = coordinationOptions.Value.TokenRefreshLockExpiration;
    }

    /// <inheritdoc/>
    public async Task<TelephonyConnectionStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var name = await GetDefaultProviderNameAsync();
        var provider = await _providerResolver.GetAsync();

        var status = new TelephonyConnectionStatus
        {
            ProviderName = name,
        };

        if (provider is null)
        {
            // No provider is configured or enabled, so the soft phone is not available.
            status.IsAvailable = false;
            status.IsConnected = false;

            return status;
        }

        status.IsAvailable = true;

        if (provider is not ITelephonyAuthenticationProvider authenticationProvider || !authenticationProvider.RequiresUserAuthentication)
        {
            // The provider uses shared, account-level credentials, so per-user authentication is not required.
            status.RequiresAuthentication = false;
            status.IsConnected = true;

            return status;
        }

        status.RequiresAuthentication = true;
        status.AuthenticationScheme = authenticationProvider.AuthenticationScheme;

        // Attempt to obtain valid tokens, refreshing them automatically when possible, so the user is
        // only asked to authenticate when there are no usable tokens. A persistence failure while
        // refreshing must not fault the status probe; it degrades to "not connected" and is logged by
        // the user accessor.
        TelephonyUserTokens tokens = null;

        if (!string.IsNullOrEmpty(name))
        {
            try
            {
                tokens = await GetValidTokensAsync(name, cancellationToken);
            }
            catch (TelephonyUserPersistenceException)
            {
                tokens = null;
            }
        }

        status.IsConnected = tokens is not null && !string.IsNullOrEmpty(tokens.AccessToken);

        return status;
    }

    /// <inheritdoc/>
    public async Task<TelephonyAuthorizationRequest> GetAuthorizationUrlAsync(string redirectUri, string state, CancellationToken cancellationToken = default)
    {
        var provider = await _providerResolver.GetAsync();

        if (provider is not ITelephonyAuthenticationProvider authenticationProvider || !authenticationProvider.RequiresUserAuthentication)
        {
            return null;
        }

        var context = new TelephonyAuthorizationContext
        {
            RedirectUri = redirectUri,
            State = state,
        };

        string codeVerifier = null;

        if (authenticationProvider.SupportsProofKeyForCodeExchange)
        {
            codeVerifier = TelephonyPkceGenerator.CreateCodeVerifier();
            context.CodeChallenge = TelephonyPkceGenerator.CreateCodeChallenge(codeVerifier);
            context.CodeChallengeMethod = TelephonyPkceGenerator.Sha256Method;
        }

        var url = await authenticationProvider.GetAuthorizationUrlAsync(context, cancellationToken);

        if (string.IsNullOrEmpty(url))
        {
            return null;
        }

        return new TelephonyAuthorizationRequest
        {
            Url = url,
            CodeVerifier = codeVerifier,
        };
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> CompleteAuthorizationAsync(string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(code))
        {
            return TelephonyResult.Failed("The authorization code is required.");
        }

        var name = await GetDefaultProviderNameAsync();
        var provider = await _providerResolver.GetAsync();

        if (string.IsNullOrEmpty(name) || provider is not ITelephonyAuthenticationProvider authenticationProvider)
        {
            return TelephonyResult.Failed("No telephony authentication provider is configured.");
        }

        var context = new TelephonyCodeExchangeContext
        {
            Code = code,
            RedirectUri = redirectUri,
            CodeVerifier = codeVerifier,
        };

        var tokens = await authenticationProvider.ExchangeCodeAsync(context, cancellationToken);

        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return TelephonyResult.Failed("The telephony provider did not return a valid access token.");
        }

        tokens.ProviderName = name;

        try
        {
            await _tokenStore.StoreAsync(name, tokens, cancellationToken);
        }
        catch (TelephonyUserPersistenceException)
        {
            return TelephonyResult.Failed("The telephony connection could not be saved. Please try connecting again.");
        }

        return TelephonyResult.Success();
    }

    /// <inheritdoc/>
    public async Task<TelephonyResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var name = await GetDefaultProviderNameAsync();

        if (string.IsNullOrEmpty(name))
        {
            return TelephonyResult.Success();
        }

        var tokens = await _tokenStore.GetAsync(name, cancellationToken);

        // Clear the local interactive credentials first and commit the deletion durably before attempting
        // the remote revocation, so the user is disconnected locally immediately, concurrent requests can
        // no longer observe the credentials, and a canceled or failing remote call cannot leave the local
        // tokens behind.
        await _tokenStore.RemoveAsync(name, cancellationToken);
        await _userAccessor.SaveChangesAsync();

        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return TelephonyResult.Success();
        }

        var provider = await _providerResolver.GetAsync(name);

        if (provider is not ITelephonyAuthenticationProvider authenticationProvider)
        {
            // A live token existed but the provider is no longer available to revoke it, so the remote
            // grant may still be active. Report the indeterminate outcome instead of a false success.
            _logger.LogWarning(
                "Telephony provider '{ProviderName}' is unavailable to revoke the disconnected user's grant. The local connection was cleared, but the remote grant may still be active.",
                name);

            return TelephonyResult.Unknown("The telephony provider was unavailable to revoke the remote grant.");
        }

        // Revoke the tokens at the provider after the local copy is removed so the provider does not keep
        // issuing API keys on behalf of the disconnected user.
        var revocation = await authenticationProvider.RevokeTokensAsync(tokens, cancellationToken);

        if (!revocation.Succeeded)
        {
            _logger.LogWarning(
                "Telephony provider '{ProviderName}' did not confirm revocation of the disconnected user's grant. The local connection was cleared, but the remote grant may still be active. Reason: {Reason}",
                name,
                revocation.Error);
        }

        return revocation;
    }

    /// <inheritdoc/>
    public async Task<TelephonyUserTokens> GetValidTokensAsync(string providerName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(providerName))
        {
            return null;
        }

        var tokens = await _tokenStore.GetAsync(providerName, cancellationToken);

        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return null;
        }

        if (!IsExpired(tokens))
        {
            return tokens;
        }

        if (string.IsNullOrEmpty(tokens.RefreshToken))
        {
            return null;
        }

        var provider = await _providerResolver.GetAsync(providerName);

        if (provider is not ITelephonyAuthenticationProvider authenticationProvider)
        {
            return null;
        }

        return await RefreshTokensUnderLockAsync(providerName, authenticationProvider, tokens, cancellationToken);
    }

    private async Task<TelephonyUserTokens> RefreshTokensUnderLockAsync(
        string providerName,
        ITelephonyAuthenticationProvider authenticationProvider,
        TelephonyUserTokens expiredTokens,
        CancellationToken cancellationToken)
    {
        var user = await _userAccessor.GetCurrentUserAsync();
        var lockKey = $"Telephony:TokenRefresh:{providerName}:{user?.UserName ?? providerName}";

        (var locker, var locked) = await _distributedLock.TryAcquireLockAsync(
            lockKey,
            _tokenRefreshLockTimeout,
            _tokenRefreshLockExpiration);

        if (!locked)
        {
            // A peer holds the refresh lock and did not release it within the wait window. Reload the user from
            // the database (bypassing this scope's cached copy) in case the peer just committed a refresh;
            // otherwise give up rather than starting a competing refresh that would rotate the peer's
            // replacement refresh token out from under it.
            await _userAccessor.ReloadCurrentUserAsync();
            var contended = await _tokenStore.GetAsync(providerName, cancellationToken);

            return IsUsable(contended) ? contended : null;
        }

        await using var acquiredLock = locker;

        // Reload the user from the database before the re-read so a peer's committed refresh is observed rather
        // than the stale tokens this request loaded before waiting for the lock. Without this the identity map
        // would keep serving the pre-refresh copy and the double-check below could rotate the token again.
        await _userAccessor.ReloadCurrentUserAsync();

        var current = await _tokenStore.GetAsync(providerName, cancellationToken);

        if (IsUsable(current))
        {
            return current;
        }

        // Refresh from the newest stored refresh token when one exists, falling back to the tokens the caller
        // already read, so a concurrent partial update is never ignored.
        var source = current is not null && !string.IsNullOrEmpty(current.RefreshToken) ? current : expiredTokens;

        if (string.IsNullOrEmpty(source.RefreshToken))
        {
            return null;
        }

        var refreshed = await authenticationProvider.RefreshTokensAsync(source, cancellationToken);

        if (refreshed is null || string.IsNullOrEmpty(refreshed.AccessToken))
        {
            return null;
        }

        refreshed.ProviderName = providerName;

        await _tokenStore.StoreAsync(providerName, refreshed, cancellationToken);

        // Commit the refreshed tokens durably before releasing the lock so a waiting peer that reloads the user
        // observes them and reuses them instead of rotating the refresh token a second time. Without this the
        // write would only commit at the end of the request scope, after the lock has already been released.
        await _userAccessor.SaveChangesAsync();

        return refreshed;
    }

    private bool IsUsable(TelephonyUserTokens tokens)
        => tokens is not null && !string.IsNullOrEmpty(tokens.AccessToken) && !IsExpired(tokens);

    private bool IsExpired(TelephonyUserTokens tokens)
    {
        if (!tokens.ExpiresUtc.HasValue)
        {
            return false;
        }

        // Treat tokens that expire within the next 30 seconds as expired to avoid race conditions.
        return _clock.UtcNow >= tokens.ExpiresUtc.Value.UtcDateTime.AddSeconds(-30);
    }

    private async Task<string> GetDefaultProviderNameAsync()
    {
        var settings = await _siteService.GetSettingsAsync<TelephonySettings>();

        return settings.DefaultProviderName;
    }
}
