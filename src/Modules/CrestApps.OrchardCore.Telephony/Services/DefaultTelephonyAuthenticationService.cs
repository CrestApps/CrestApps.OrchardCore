using CrestApps.OrchardCore.Telephony.Models;
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
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTelephonyAuthenticationService"/> class.
    /// </summary>
    /// <param name="siteService">The site service used to read the default provider name.</param>
    /// <param name="providerResolver">The telephony provider resolver.</param>
    /// <param name="tokenStore">The user token store.</param>
    /// <param name="clock">The clock used to evaluate token expiration.</param>
    public DefaultTelephonyAuthenticationService(
        ISiteService siteService,
        ITelephonyProviderResolver providerResolver,
        ITelephonyUserTokenStore tokenStore,
        IClock clock)
    {
        _siteService = siteService;
        _providerResolver = providerResolver;
        _tokenStore = tokenStore;
        _clock = clock;
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
        // only asked to authenticate when there are no usable tokens.
        var tokens = string.IsNullOrEmpty(name) ? null : await GetValidTokensAsync(name, cancellationToken);
        status.IsConnected = tokens is not null && !string.IsNullOrEmpty(tokens.AccessToken);

        return status;
    }

    /// <inheritdoc/>
    public async Task<string> GetAuthorizationUrlAsync(string redirectUri, string state, CancellationToken cancellationToken = default)
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

        return await authenticationProvider.GetAuthorizationUrlAsync(context, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CompleteAuthorizationAsync(string code, string redirectUri, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(code))
        {
            return false;
        }

        var name = await GetDefaultProviderNameAsync();
        var provider = await _providerResolver.GetAsync();

        if (string.IsNullOrEmpty(name) || provider is not ITelephonyAuthenticationProvider authenticationProvider)
        {
            return false;
        }

        var context = new TelephonyCodeExchangeContext
        {
            Code = code,
            RedirectUri = redirectUri,
        };

        var tokens = await authenticationProvider.ExchangeCodeAsync(context, cancellationToken);

        if (tokens is null || string.IsNullOrEmpty(tokens.AccessToken))
        {
            return false;
        }

        tokens.ProviderName = name;

        await _tokenStore.StoreAsync(name, tokens, cancellationToken);

        return true;
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        var name = await GetDefaultProviderNameAsync();

        if (!string.IsNullOrEmpty(name))
        {
            await _tokenStore.RemoveAsync(name, cancellationToken);
        }
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

        var refreshed = await authenticationProvider.RefreshTokensAsync(tokens, cancellationToken);

        if (refreshed is null || string.IsNullOrEmpty(refreshed.AccessToken))
        {
            return null;
        }

        refreshed.ProviderName = providerName;

        await _tokenStore.StoreAsync(providerName, refreshed, cancellationToken);

        return refreshed;
    }

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
