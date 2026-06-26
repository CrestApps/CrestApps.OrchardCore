using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Default provider dispatcher for phone number verification requests.
/// </summary>
public sealed class DefaultPhoneNumberVerificationManager : IPhoneNumberVerificationManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PhoneNumberVerificationProviderOptions _providerOptions;
    private readonly ISiteService _siteService;
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
    /// <param name="handlers">The verification lifecycle handlers.</param>
    /// <param name="phoneNumberService">The phone number formatting service.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultPhoneNumberVerificationManager(
        IServiceProvider serviceProvider,
        IOptions<PhoneNumberVerificationProviderOptions> providerOptions,
        ISiteService siteService,
        IEnumerable<IPhoneNumberVerificationHandler> handlers,
        IPhoneNumberService phoneNumberService,
        IClock clock,
        ILogger<DefaultPhoneNumberVerificationManager> logger)
    {
        _serviceProvider = serviceProvider;
        _providerOptions = providerOptions.Value;
        _siteService = siteService;
        _handlers = handlers;
        _phoneNumberService = phoneNumberService;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor> GetProviders()
        => _providerOptions.Providers.Values;

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
        if (_providerOptions.Providers.Count == 0)
        {
            return null;
        }

        var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

        if (!string.IsNullOrEmpty(settings.SelectedProvider) && _providerOptions.Providers.ContainsKey(settings.SelectedProvider))
        {
            return settings.SelectedProvider;
        }

        return _providerOptions.Providers.Keys.First();
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
                ?? CreateFailedResult(phoneNumber, normalizedPhoneNumber, providerKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "The phone number verification provider '{ProviderKey}' failed to verify a phone number.", providerKey);

            result = CreateFailedResult(phoneNumber, normalizedPhoneNumber, providerKey);
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

    private PhoneNumberVerificationResult CreateFailedResult(string phoneNumber, string normalizedPhoneNumber, string providerKey)
    {
        return new PhoneNumberVerificationResult
        {
            PhoneNumber = phoneNumber,
            NormalizedPhoneNumber = normalizedPhoneNumber,
            VerificationProvider = providerKey,
            VerificationDateUtc = _clock.UtcNow,
            Status = PhoneNumberVerificationStatus.Failed,
            LineType = PhoneNumberLineType.Unknown,
        };
    }
}
