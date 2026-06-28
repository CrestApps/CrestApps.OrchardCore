using CrestApps.OrchardCore.PhoneNumbers.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumbers.Core.Services;

/// <summary>
/// Default provider dispatcher for phone number verification requests.
/// </summary>
public sealed class DefaultPhoneNumberVerificationManager : IPhoneNumberVerificationManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PhoneNumberVerificationProviderOptions _providerOptions;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<IPhoneNumberVerificationProviderConfiguration> _providerConfigurations;
    private readonly IEnumerable<IPhoneNumberVerificationHandler> _handlers;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPhoneNumberVerificationManager"/> class.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider used to resolve providers by key.</param>
    /// <param name="providerOptions">The registered provider descriptors.</param>
    /// <param name="siteService">The site service used to read module settings.</param>
    /// <param name="providerConfigurations">The provider enabled-state configurations.</param>
    /// <param name="handlers">The verification lifecycle handlers.</param>
    /// <param name="phoneNumberService">The phone number formatting service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultPhoneNumberVerificationManager(
        IServiceProvider serviceProvider,
        IOptions<PhoneNumberVerificationProviderOptions> providerOptions,
        ISiteService siteService,
        IEnumerable<IPhoneNumberVerificationProviderConfiguration> providerConfigurations,
        IEnumerable<IPhoneNumberVerificationHandler> handlers,
        IPhoneNumberService phoneNumberService,
        IClock clock,
        ILogger<DefaultPhoneNumberVerificationManager> logger)
    {
        _serviceProvider = serviceProvider;
        _providerOptions = providerOptions.Value;
        _siteService = siteService;
        _providerConfigurations = providerConfigurations;
        _handlers = handlers;
        _phoneNumberService = phoneNumberService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor> GetProviders()
        => _providerOptions.Providers.Values;

    /// <inheritdoc/>
    public async Task<IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor>> GetEnabledProvidersAsync(CancellationToken cancellationToken = default)
    {
        if (_providerOptions.Providers.Count == 0)
        {
            return [];
        }

        var enabled = new List<PhoneNumberVerificationProviderDescriptor>();

        foreach (var descriptor in _providerOptions.Providers.Values)
        {
            if (await IsProviderEnabledAsync(descriptor.Key, cancellationToken))
            {
                enabled.Add(descriptor);
            }
        }

        return enabled;
    }

    /// <inheritdoc/>
    public bool TryGetProvider(string key, out IPhoneNumberVerificationProvider provider)
    {
        provider = null;

        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        provider = _serviceProvider.GetKeyedService<IPhoneNumberVerificationProvider>(key);

        return provider is not null;
    }

    /// <inheritdoc/>
    public async Task<string> GetDefaultProviderKeyAsync(CancellationToken cancellationToken = default)
    {
        var enabledProviders = await GetEnabledProvidersAsync(cancellationToken);

        if (enabledProviders.Count == 0)
        {
            return null;
        }

        var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

        if (!string.IsNullOrEmpty(settings.SelectedProvider)
            && enabledProviders.Any(provider => string.Equals(provider.Key, settings.SelectedProvider, StringComparison.OrdinalIgnoreCase)))
        {
            return settings.SelectedProvider;
        }

        return enabledProviders.First().Key;
    }

    private async Task<bool> IsProviderEnabledAsync(string key, CancellationToken cancellationToken)
    {
        var hasConfiguration = false;

        foreach (var configuration in _providerConfigurations)
        {
            if (!string.Equals(configuration.ProviderKey, key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            hasConfiguration = true;

            if (await configuration.IsEnabledAsync(cancellationToken))
            {
                return true;
            }
        }

        // Providers without an explicit enabled-state configuration (for example, simple providers
        // that do not expose settings) are always considered available.
        return !hasConfiguration;
    }

    /// <inheritdoc/>
    public async Task<PhoneNumberVerificationResult> VerifyAsync(
        string phoneNumber,
        string providerKey = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        providerKey ??= await GetDefaultProviderKeyAsync(cancellationToken);

        if (string.IsNullOrEmpty(providerKey))
        {
            throw new InvalidOperationException("No phone number verification provider is registered.");
        }

        if (!TryGetProvider(providerKey, out var provider))
        {
            throw new InvalidOperationException($"No phone number verification provider is registered for the key '{providerKey}'.");
        }

        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber);

        var context = new PhoneNumberVerificationContext
        {
            PhoneNumber = phoneNumber,
            ProviderKey = providerKey,
        };

        await _handlers.InvokeAsync((handler, ctx) => handler.VerifyingAsync(ctx, cancellationToken), context, _logger);

        PhoneNumberVerificationResult result;

        try
        {
            result = await provider.VerifyAsync(normalizedPhoneNumber, cancellationToken)
                ?? CreateFailedResult(phoneNumber, normalizedPhoneNumber, providerKey, "The verification provider returned no result.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The phone number verification provider '{ProviderKey}' failed to verify a phone number.", providerKey);

            result = CreateFailedResult(phoneNumber, normalizedPhoneNumber, providerKey, ex.Message);
        }

        result.PhoneNumber ??= phoneNumber;
        result.NormalizedPhoneNumber ??= normalizedPhoneNumber;
        result.VerificationProvider = providerKey;

        if (result.VerificationDateUtc == default)
        {
            result.VerificationDateUtc = _clock.UtcNow;
        }

        context.Result = result;

        await _handlers.InvokeAsync((handler, ctx) => handler.VerifiedAsync(ctx, cancellationToken), context, _logger);

        return result;
    }

    private string NormalizePhoneNumber(string phoneNumber)
    {
        if (_phoneNumberService.TryFormatToE164(phoneNumber, regionCode: null, out var e164Number))
        {
            return e164Number;
        }

        return phoneNumber;
    }

    private PhoneNumberVerificationResult CreateFailedResult(string phoneNumber, string normalizedPhoneNumber, string providerKey, string errorMessage)
    {
        return new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = normalizedPhoneNumber,
            VerificationProvider = providerKey,
            VerificationDateUtc = _clock.UtcNow,
            Status = PhoneNumberVerificationStatus.Failed,
            LineType = PhoneNumberLineType.Unknown,
            ErrorMessage = errorMessage,
        };
    }
}
