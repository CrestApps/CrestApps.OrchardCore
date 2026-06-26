using CrestApps.OrchardCore.PhoneNumberVerifications.Models;
using CrestApps.OrchardCore.PhoneNumbers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.PhoneNumberVerifications.Services;

/// <summary>
/// Default implementation of <see cref="IPhoneNumberVerificationManager"/>.
/// </summary>
public sealed class DefaultPhoneNumberVerificationManager : IPhoneNumberVerificationManager
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PhoneNumberVerificationProviderOptions _providerOptions;
    private readonly ISiteService _siteService;
    private readonly IPhoneNumberVerificationStore _store;
    private readonly IEnumerable<IContentPhoneNumberResolver> _phoneNumberResolvers;
    private readonly IEnumerable<IPhoneNumberVerificationHandler> _handlers;
    private readonly IPhoneNumberService _phoneNumberService;
    private readonly ISession _session;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultPhoneNumberVerificationManager"/> class.
    /// </summary>
    /// <param name="serviceProvider">The scoped service provider used to resolve providers by key.</param>
    /// <param name="providerOptions">The registered provider descriptors.</param>
    /// <param name="siteService">The site service used to read module settings.</param>
    /// <param name="store">The verification store.</param>
    /// <param name="phoneNumberResolvers">The content phone number resolvers.</param>
    /// <param name="handlers">The verification lifecycle handlers.</param>
    /// <param name="phoneNumberService">The phone number formatting service.</param>
    /// <param name="session">The YesSql session used to persist content item changes.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    public DefaultPhoneNumberVerificationManager(
        IServiceProvider serviceProvider,
        IOptions<PhoneNumberVerificationProviderOptions> providerOptions,
        ISiteService siteService,
        IPhoneNumberVerificationStore store,
        IEnumerable<IContentPhoneNumberResolver> phoneNumberResolvers,
        IEnumerable<IPhoneNumberVerificationHandler> handlers,
        IPhoneNumberService phoneNumberService,
        ISession session,
        IClock clock,
        ILogger<DefaultPhoneNumberVerificationManager> logger)
    {
        _serviceProvider = serviceProvider;
        _providerOptions = providerOptions.Value;
        _siteService = siteService;
        _store = store;
        _phoneNumberResolvers = phoneNumberResolvers;
        _handlers = handlers;
        _phoneNumberService = phoneNumberService;
        _session = session;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<PhoneNumberVerificationProviderDescriptor> GetProviders()
        => _providerOptions.Providers.Values.ToArray();

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
    public Task<PhoneNumberVerificationResult> VerifyAsync(
        string phoneNumber,
        string providerKey = null,
        CancellationToken cancellationToken = default)
        => VerifyCoreAsync(phoneNumber, providerKey, contentItem: null, cancellationToken);

    /// <inheritdoc/>
    public async Task<PhoneNumberVerificationResult> VerifyContentItemAsync(
        ContentItem contentItem,
        PhoneNumberVerificationOptions options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        options ??= new PhoneNumberVerificationOptions();

        if (!options.Force && _store.IsVerified(contentItem) && !await _store.RequiresRevalidationAsync(contentItem, cancellationToken))
        {
            return _store.Read(contentItem);
        }

        var phoneNumber = await ResolvePhoneNumberAsync(contentItem);

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            return null;
        }

        var result = await VerifyCoreAsync(phoneNumber, options.ProviderKey, contentItem, cancellationToken);

        await _store.UpdateAsync(contentItem, result, options.VerifiedByUserId, cancellationToken);
        await _session.SaveAsync(contentItem);

        return result;
    }

    /// <inheritdoc/>
    public async Task<PhoneNumberVerificationResult> EnsureVerifiedAsync(
        ContentItem contentItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        var settings = await _siteService.GetSettingsAsync<PhoneNumberVerificationsSettings>();

        if (!settings.EnableJustInTimeVerification)
        {
            return _store.Read(contentItem);
        }

        if (await _store.RequiresRevalidationAsync(contentItem, cancellationToken))
        {
            return await VerifyContentItemAsync(contentItem, options: null, cancellationToken);
        }

        return _store.Read(contentItem);
    }

    private async Task<PhoneNumberVerificationResult> VerifyCoreAsync(
        string phoneNumber,
        string providerKey,
        ContentItem contentItem,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(phoneNumber);

        providerKey ??= await GetDefaultProviderKeyAsync(cancellationToken);

        if (!TryGetProvider(providerKey, out var provider))
        {
            throw new InvalidOperationException($"No phone number verification provider is registered for the key '{providerKey}'.");
        }

        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber);

        var context = new PhoneNumberVerificationContext
        {
            PhoneNumber = phoneNumber,
            ProviderKey = providerKey,
            ContentItem = contentItem,
        };

        await _handlers.InvokeAsync((handler, ctx) => handler.VerifyingAsync(ctx), context, _logger);

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

        await _handlers.InvokeAsync((handler, ctx) => handler.VerifiedAsync(ctx), context, _logger);

        return result;
    }

    private async Task<string> ResolvePhoneNumberAsync(ContentItem contentItem)
    {
        var existing = _store.Read(contentItem);
        var phoneNumber = existing?.NormalizedPhoneNumber ?? existing?.PhoneNumber;

        if (!string.IsNullOrWhiteSpace(phoneNumber))
        {
            return phoneNumber;
        }

        foreach (var resolver in _phoneNumberResolvers)
        {
            phoneNumber = await resolver.GetPhoneNumberAsync(contentItem);

            if (!string.IsNullOrWhiteSpace(phoneNumber))
            {
                return phoneNumber;
            }
        }

        return null;
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
